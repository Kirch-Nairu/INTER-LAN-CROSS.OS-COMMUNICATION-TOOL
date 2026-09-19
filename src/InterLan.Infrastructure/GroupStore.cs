using InterLan.Contracts;
using InterLan.Domain;
using Microsoft.Data.Sqlite;

namespace InterLan.Infrastructure;

public sealed class GroupStore(SqliteDatabase database)
{
    public async Task<GroupDetailsResponse> CreateGroupAsync(
        Guid actorUserId,
        CreateGroupRequest request,
        CancellationToken cancellationToken = default)
    {
        var name = GroupTextPolicy.NormalizeName(request.Name);
        var topic = GroupTextPolicy.NormalizeTopic(request.Topic);

        await using var connection = database.OpenConnection();
        await RequireActiveUserAsync(
            connection,
            actorUserId,
            cancellationToken);

        using var transaction = connection.BeginTransaction();
        var groupId = Guid.NewGuid();
        var now = DateTimeOffset.UtcNow;

        await using (var group = connection.CreateCommand())
        {
            group.Transaction = transaction;
            group.CommandText =
                """
                INSERT INTO groups (
                    group_id, name, topic, created_by_user_id, created_utc
                ) VALUES (
                    $groupId, $name, $topic, $creator, $createdUtc
                );
                """;
            group.Parameters.AddWithValue("$groupId", groupId.ToString("D"));
            group.Parameters.AddWithValue("$name", name);
            group.Parameters.AddWithValue(
                "$topic",
                topic is null ? DBNull.Value : topic);
            group.Parameters.AddWithValue("$creator", actorUserId.ToString("D"));
            group.Parameters.AddWithValue("$createdUtc", now.ToString("O"));
            await group.ExecuteNonQueryAsync(cancellationToken);
        }

        await using (var owner = connection.CreateCommand())
        {
            owner.Transaction = transaction;
            owner.CommandText =
                """
                INSERT INTO group_members (
                    group_id, user_id, group_role, joined_utc, removed_utc
                ) VALUES (
                    $groupId, $userId, 'OWNER', $joinedUtc, NULL
                );
                """;
            owner.Parameters.AddWithValue("$groupId", groupId.ToString("D"));
            owner.Parameters.AddWithValue("$userId", actorUserId.ToString("D"));
            owner.Parameters.AddWithValue("$joinedUtc", now.ToString("O"));
            await owner.ExecuteNonQueryAsync(cancellationToken);
        }

        await AppendGroupEventAsync(
            connection,
            transaction,
            groupId,
            actorUserId,
            actorUserId,
            "GROUP_CREATED",
            "{}",
            now,
            cancellationToken);

        await AppendAuditAsync(
            connection,
            transaction,
            actorUserId,
            "GROUP_CREATED",
            groupId,
            "{}",
            now,
            cancellationToken);

        transaction.Commit();

        return await GetGroupDetailsAsync(
            actorUserId,
            groupId,
            cancellationToken);
    }

    public async Task<GroupDetailsResponse> GetGroupDetailsAsync(
        Guid actorUserId,
        Guid groupId,
        CancellationToken cancellationToken = default)
    {
        await using var connection = database.OpenConnection();
        var myRole = await RequireActiveGroupMemberAsync(
            connection,
            actorUserId,
            groupId,
            cancellationToken);

        string name;
        string? topic;
        Guid creator;
        DateTimeOffset createdUtc;

        await using (var group = connection.CreateCommand())
        {
            group.CommandText =
                """
                SELECT name, topic, created_by_user_id, created_utc
                FROM groups
                WHERE group_id = $groupId;
                """;
            group.Parameters.AddWithValue("$groupId", groupId.ToString("D"));

            await using var reader = await group.ExecuteReaderAsync(cancellationToken);
            if (!await reader.ReadAsync(cancellationToken))
                throw new KeyNotFoundException("Group not found.");

            name = reader.GetString(0);
            topic = reader.IsDBNull(1) ? null : reader.GetString(1);
            creator = Guid.Parse(reader.GetString(2));
            createdUtc = DateTimeOffset.Parse(reader.GetString(3));
        }

        var members = new List<GroupMemberResponse>();
        await using (var member = connection.CreateCommand())
        {
            member.CommandText =
                """
                SELECT u.user_id, u.username, u.display_name,
                       gm.group_role, gm.joined_utc
                FROM group_members gm
                JOIN users u ON u.user_id = gm.user_id
                WHERE gm.group_id = $groupId
                  AND gm.removed_utc IS NULL
                  AND u.disabled_utc IS NULL
                ORDER BY
                    CASE gm.group_role
                        WHEN 'OWNER' THEN 0
                        WHEN 'ADMIN' THEN 1
                        ELSE 2
                    END,
                    lower(u.display_name),
                    u.user_id;
                """;
            member.Parameters.AddWithValue("$groupId", groupId.ToString("D"));

            await using var reader = await member.ExecuteReaderAsync(cancellationToken);
            while (await reader.ReadAsync(cancellationToken))
            {
                members.Add(new GroupMemberResponse(
                    Guid.Parse(reader.GetString(0)),
                    reader.GetString(1),
                    reader.GetString(2),
                    reader.GetString(3),
                    DateTimeOffset.Parse(reader.GetString(4))));
            }
        }

        return new GroupDetailsResponse(
            groupId,
            name,
            topic,
            creator,
            createdUtc,
            myRole,
            members);
    }

    private static async Task AppendGroupEventAsync(
        SqliteConnection connection,
        SqliteTransaction transaction,
        Guid groupId,
        Guid? actorUserId,
        Guid? subjectUserId,
        string eventType,
        string payloadJson,
        DateTimeOffset createdUtc,
        CancellationToken cancellationToken)
    {
        await using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText =
            """
            INSERT INTO group_events (
                group_event_id, group_id, actor_user_id,
                subject_user_id, event_type, payload_json, created_utc
            ) VALUES (
                $eventId, $groupId, $actorUserId,
                $subjectUserId, $eventType, $payloadJson, $createdUtc
            );
            """;
        command.Parameters.AddWithValue("$eventId", Guid.NewGuid().ToString("D"));
        command.Parameters.AddWithValue("$groupId", groupId.ToString("D"));
        command.Parameters.AddWithValue(
            "$actorUserId",
            actorUserId is null ? DBNull.Value : actorUserId.Value.ToString("D"));
        command.Parameters.AddWithValue(
            "$subjectUserId",
            subjectUserId is null ? DBNull.Value : subjectUserId.Value.ToString("D"));
        command.Parameters.AddWithValue("$eventType", eventType);
        command.Parameters.AddWithValue("$payloadJson", payloadJson);
        command.Parameters.AddWithValue("$createdUtc", createdUtc.ToString("O"));
        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    private static async Task AppendAuditAsync(
        SqliteConnection connection,
        SqliteTransaction transaction,
        Guid actorUserId,
        string eventType,
        Guid groupId,
        string payloadJson,
        DateTimeOffset createdUtc,
        CancellationToken cancellationToken)
    {
        await using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText =
            """
            INSERT INTO audit_events (
                audit_event_id, actor_user_id, event_type,
                subject_type, subject_id, payload_json, created_utc
            ) VALUES (
                $auditId, $actorUserId, $eventType,
                'GROUP', $groupId, $payloadJson, $createdUtc
            );
            """;
        command.Parameters.AddWithValue("$auditId", Guid.NewGuid().ToString("D"));
        command.Parameters.AddWithValue("$actorUserId", actorUserId.ToString("D"));
        command.Parameters.AddWithValue("$eventType", eventType);
        command.Parameters.AddWithValue("$groupId", groupId.ToString("D"));
        command.Parameters.AddWithValue("$payloadJson", payloadJson);
        command.Parameters.AddWithValue("$createdUtc", createdUtc.ToString("O"));
        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    private static async Task RequireActiveUserAsync(
        SqliteConnection connection,
        Guid userId,
        CancellationToken cancellationToken)
    {
        await using var command = connection.CreateCommand();
        command.CommandText =
            """
            SELECT COUNT(1)
            FROM users
            WHERE user_id = $userId
              AND disabled_utc IS NULL;
            """;
        command.Parameters.AddWithValue("$userId", userId.ToString("D"));

        if (Convert.ToInt32(await command.ExecuteScalarAsync(cancellationToken)) != 1)
            throw new UnauthorizedAccessException("Active user is required.");
    }

    private static async Task<string> RequireActiveGroupMemberAsync(
        SqliteConnection connection,
        Guid actorUserId,
        Guid groupId,
        CancellationToken cancellationToken,
        SqliteTransaction? transaction = null)
    {
        await using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText =
            """
            SELECT gm.group_role
            FROM group_members gm
            JOIN users u ON u.user_id = gm.user_id
            WHERE gm.group_id = $groupId
              AND gm.user_id = $userId
              AND gm.removed_utc IS NULL
              AND u.disabled_utc IS NULL;
            """;
        command.Parameters.AddWithValue("$groupId", groupId.ToString("D"));
        command.Parameters.AddWithValue("$userId", actorUserId.ToString("D"));

        var role = Convert.ToString(
            await command.ExecuteScalarAsync(cancellationToken));

        return !string.IsNullOrWhiteSpace(role)
            ? role
            : throw new UnauthorizedAccessException("Active group membership is required.");
    }
}
