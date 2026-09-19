using InterLan.Contracts;
using InterLan.Domain;
using Microsoft.Data.Sqlite;

namespace InterLan.Infrastructure;

public sealed class GroupStore(SqliteDatabase database)
{
    public const int MaxMessageLength = 4_000;
    public const int MaxMessagePageSize = 200;

    private static SqliteTransaction BeginWriteTransaction(SqliteConnection connection) =>
        connection.BeginTransaction(deferred: false);

    private static SqliteTransaction BeginReadTransaction(SqliteConnection connection) =>
        connection.BeginTransaction(deferred: true);

    public async Task<GroupDetailsResponse> CreateGroupAsync(
        Guid actorUserId,
        CreateGroupRequest request,
        CancellationToken cancellationToken = default)
    {
        var name = GroupTextPolicy.NormalizeName(request.Name);
        var topic = GroupTextPolicy.NormalizeTopic(request.Topic);

        await using var connection = database.OpenConnection();
        using var transaction = BeginWriteTransaction(connection);

        await RequireActiveUserAsync(
            connection,
            actorUserId,
            cancellationToken,
            transaction);

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

        var created = await GetGroupDetailsAsync(
            connection,
            transaction,
            actorUserId,
            groupId,
            cancellationToken);

        transaction.Commit();
        return created;
    }

    public async Task<IReadOnlyList<GroupSummaryResponse>> ListGroupsAsync(
        Guid actorUserId,
        CancellationToken cancellationToken = default)
    {
        await using var connection = database.OpenConnection();
        using var transaction = BeginReadTransaction(connection);

        await RequireActiveUserAsync(
            connection,
            actorUserId,
            cancellationToken,
            transaction);

        await using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText =
            """
            SELECT g.group_id, g.name, g.topic,
                   gm.group_role, g.created_utc
            FROM groups g
            JOIN group_members gm
              ON gm.group_id = g.group_id
             AND gm.user_id = $userId
             AND gm.removed_utc IS NULL
            ORDER BY lower(g.name), g.group_id;
            """;
        command.Parameters.AddWithValue("$userId", actorUserId.ToString("D"));

        var groups = new List<GroupSummaryResponse>();
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            groups.Add(new GroupSummaryResponse(
                Guid.Parse(reader.GetString(0)),
                reader.GetString(1),
                reader.IsDBNull(2) ? null : reader.GetString(2),
                reader.GetString(3),
                DateTimeOffset.Parse(reader.GetString(4))));
        }

        transaction.Commit();
        return groups;
    }

    public async Task<GroupDetailsResponse> GetGroupDetailsAsync(
        Guid actorUserId,
        Guid groupId,
        CancellationToken cancellationToken = default)
    {
        await using var connection = database.OpenConnection();
        using var transaction = BeginReadTransaction(connection);

        var details = await GetGroupDetailsAsync(
            connection,
            transaction,
            actorUserId,
            groupId,
            cancellationToken);

        transaction.Commit();
        return details;
    }

    private static async Task<GroupDetailsResponse> GetGroupDetailsAsync(
        SqliteConnection connection,
        SqliteTransaction? transaction,
        Guid actorUserId,
        Guid groupId,
        CancellationToken cancellationToken)
    {
        var myRole = await RequireActiveGroupMemberAsync(
            connection,
            actorUserId,
            groupId,
            cancellationToken,
            transaction);

        string name;
        string? topic;
        Guid creator;
        DateTimeOffset createdUtc;

        await using (var group = connection.CreateCommand())
        {
            group.Transaction = transaction;
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
            member.Transaction = transaction;
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

    public async Task<GroupDetailsResponse> UpdateGroupAsync(
        Guid actorUserId,
        Guid groupId,
        UpdateGroupRequest request,
        CancellationToken cancellationToken = default)
    {
        var name = GroupTextPolicy.NormalizeName(request.Name);
        var topic = GroupTextPolicy.NormalizeTopic(request.Topic);

        await using var connection = database.OpenConnection();
        using var transaction = BeginWriteTransaction(connection);

        var role = await RequireActiveGroupMemberAsync(
            connection,
            actorUserId,
            groupId,
            cancellationToken,
            transaction);

        if (role is not "OWNER" and not "ADMIN")
            throw new UnauthorizedAccessException(
                "Group owner or admin authority is required.");

        var now = DateTimeOffset.UtcNow;

        await using (var update = connection.CreateCommand())
        {
            update.Transaction = transaction;
            update.CommandText =
                """
                UPDATE groups
                SET name = $name,
                    topic = $topic
                WHERE group_id = $groupId;
                """;
            update.Parameters.AddWithValue("$name", name);
            update.Parameters.AddWithValue(
                "$topic",
                topic is null ? DBNull.Value : topic);
            update.Parameters.AddWithValue("$groupId", groupId.ToString("D"));

            if (await update.ExecuteNonQueryAsync(cancellationToken) != 1)
                throw new KeyNotFoundException("Group not found.");
        }

        await AppendGroupEventAsync(
            connection,
            transaction,
            groupId,
            actorUserId,
            null,
            "GROUP_METADATA_UPDATED",
            "{}",
            now,
            cancellationToken);

        await AppendAuditAsync(
            connection,
            transaction,
            actorUserId,
            "GROUP_METADATA_UPDATED",
            groupId,
            "{}",
            now,
            cancellationToken);

        var updated = await GetGroupDetailsAsync(
            connection,
            transaction,
            actorUserId,
            groupId,
            cancellationToken);

        transaction.Commit();
        return updated;
    }

    public async Task<GroupMembershipMutationResponse> AddMemberAsync(
        Guid actorUserId,
        Guid groupId,
        AddGroupMemberRequest request,
        CancellationToken cancellationToken = default)
    {
        if (request.UserId == Guid.Empty)
            throw new ArgumentException("Group member user ID is required.", nameof(request));

        var requestedRole = NormalizeAssignableGroupRole(request.Role);

        await using var connection = database.OpenConnection();
        using var transaction = BeginWriteTransaction(connection);

        await RequireActiveUserAsync(
            connection,
            request.UserId,
            cancellationToken,
            transaction);

        var actorRole = await RequireActiveGroupMemberAsync(
            connection,
            actorUserId,
            groupId,
            cancellationToken,
            transaction);

        if (actorRole == "MEMBER")
            throw new UnauthorizedAccessException(
                "Group owner or admin authority is required to add members.");

        if (requestedRole == "ADMIN" && actorRole != "OWNER")
            throw new UnauthorizedAccessException(
                "Only the group owner can add an admin.");

        var existing = await GetGroupMemberStateAsync(
            connection,
            groupId,
            request.UserId,
            cancellationToken,
            transaction);

        if (existing is { RemovedUtc: null })
        {
            if (existing.Value.Role == requestedRole)
            {
                transaction.Commit();
                return new GroupMembershipMutationResponse(
                    groupId,
                    request.UserId,
                    requestedRole,
                    "UNCHANGED",
                    DateTimeOffset.UtcNow);
            }

            throw new InvalidOperationException(
                "User is already an active group member. Use the role mutation endpoint.");
        }

        var now = DateTimeOffset.UtcNow;

        if (existing is null)
        {
            await using var insert = connection.CreateCommand();
            insert.Transaction = transaction;
            insert.CommandText =
                """
                INSERT INTO group_members (
                    group_id, user_id, group_role, joined_utc, removed_utc
                ) VALUES (
                    $groupId, $userId, $role, $joinedUtc, NULL
                );
                """;
            insert.Parameters.AddWithValue("$groupId", groupId.ToString("D"));
            insert.Parameters.AddWithValue("$userId", request.UserId.ToString("D"));
            insert.Parameters.AddWithValue("$role", requestedRole);
            insert.Parameters.AddWithValue("$joinedUtc", now.ToString("O"));
            await insert.ExecuteNonQueryAsync(cancellationToken);
        }
        else
        {
            await using var restore = connection.CreateCommand();
            restore.Transaction = transaction;
            restore.CommandText =
                """
                UPDATE group_members
                SET group_role = $role,
                    joined_utc = $joinedUtc,
                    removed_utc = NULL
                WHERE group_id = $groupId
                  AND user_id = $userId
                  AND removed_utc IS NOT NULL;
                """;
            restore.Parameters.AddWithValue("$role", requestedRole);
            restore.Parameters.AddWithValue("$joinedUtc", now.ToString("O"));
            restore.Parameters.AddWithValue("$groupId", groupId.ToString("D"));
            restore.Parameters.AddWithValue("$userId", request.UserId.ToString("D"));

            if (await restore.ExecuteNonQueryAsync(cancellationToken) != 1)
                throw new InvalidOperationException("Removed group membership could not be restored.");
        }

        var payload = $"{{\"role\":\"{requestedRole}\"}}";

        await AppendGroupEventAsync(
            connection,
            transaction,
            groupId,
            actorUserId,
            request.UserId,
            "MEMBER_ADDED",
            payload,
            now,
            cancellationToken);

        await AppendAuditAsync(
            connection,
            transaction,
            actorUserId,
            "GROUP_MEMBER_ADDED",
            groupId,
            payload,
            now,
            cancellationToken);

        transaction.Commit();

        return new GroupMembershipMutationResponse(
            groupId,
            request.UserId,
            requestedRole,
            "ADDED",
            now);
    }

    public async Task<GroupMembershipMutationResponse> RemoveMemberAsync(
        Guid actorUserId,
        Guid groupId,
        Guid targetUserId,
        CancellationToken cancellationToken = default)
    {
        if (targetUserId == Guid.Empty)
            throw new ArgumentException("Group member user ID is required.", nameof(targetUserId));

        await using var connection = database.OpenConnection();
        using var transaction = BeginWriteTransaction(connection);

        var actorRole = await RequireActiveGroupMemberAsync(
            connection,
            actorUserId,
            groupId,
            cancellationToken,
            transaction);

        var target = await GetGroupMemberStateAsync(
            connection,
            groupId,
            targetUserId,
            cancellationToken,
            transaction);

        if (target is null || target.Value.RemovedUtc is not null)
            throw new KeyNotFoundException("Active group member was not found.");

        if (target.Value.Role == "OWNER")
            throw new UnauthorizedAccessException(
                "The group owner cannot be removed.");

        var removingSelf = actorUserId == targetUserId;
        if (!removingSelf)
        {
            if (actorRole == "MEMBER")
                throw new UnauthorizedAccessException(
                    "Group owner or admin authority is required to remove another member.");

            if (actorRole == "ADMIN" && target.Value.Role != "MEMBER")
                throw new UnauthorizedAccessException(
                    "Group admins may remove members but cannot remove other admins.");
        }

        var now = DateTimeOffset.UtcNow;

        await using (var remove = connection.CreateCommand())
        {
            remove.Transaction = transaction;
            remove.CommandText =
                """
                UPDATE group_members
                SET removed_utc = $removedUtc
                WHERE group_id = $groupId
                  AND user_id = $userId
                  AND removed_utc IS NULL;
                """;
            remove.Parameters.AddWithValue("$removedUtc", now.ToString("O"));
            remove.Parameters.AddWithValue("$groupId", groupId.ToString("D"));
            remove.Parameters.AddWithValue("$userId", targetUserId.ToString("D"));

            if (await remove.ExecuteNonQueryAsync(cancellationToken) != 1)
                throw new InvalidOperationException("Group membership removal lost its authority race.");
        }

        var payload = $"{{\"previousRole\":\"{target.Value.Role}\"}}";

        await AppendGroupEventAsync(
            connection,
            transaction,
            groupId,
            actorUserId,
            targetUserId,
            "MEMBER_REMOVED",
            payload,
            now,
            cancellationToken);

        await AppendAuditAsync(
            connection,
            transaction,
            actorUserId,
            "GROUP_MEMBER_REMOVED",
            groupId,
            payload,
            now,
            cancellationToken);

        transaction.Commit();

        return new GroupMembershipMutationResponse(
            groupId,
            targetUserId,
            null,
            "REMOVED",
            now);
    }

    public async Task<GroupMembershipMutationResponse> UpdateMemberRoleAsync(
        Guid actorUserId,
        Guid groupId,
        Guid targetUserId,
        UpdateGroupMemberRoleRequest request,
        CancellationToken cancellationToken = default)
    {
        if (targetUserId == Guid.Empty)
            throw new ArgumentException("Group member user ID is required.", nameof(targetUserId));

        var requestedRole = NormalizeAssignableGroupRole(request.Role);

        await using var connection = database.OpenConnection();
        using var transaction = BeginWriteTransaction(connection);

        var actorRole = await RequireActiveGroupMemberAsync(
            connection,
            actorUserId,
            groupId,
            cancellationToken,
            transaction);

        if (actorRole != "OWNER")
            throw new UnauthorizedAccessException(
                "Only the group owner can promote or demote members.");

        var target = await GetGroupMemberStateAsync(
            connection,
            groupId,
            targetUserId,
            cancellationToken,
            transaction);

        if (target is null || target.Value.RemovedUtc is not null)
            throw new KeyNotFoundException("Active group member was not found.");

        if (target.Value.Role == "OWNER")
            throw new UnauthorizedAccessException(
                "The group owner role cannot be changed.");

        if (target.Value.Role == requestedRole)
        {
            transaction.Commit();
            return new GroupMembershipMutationResponse(
                groupId,
                targetUserId,
                requestedRole,
                "UNCHANGED",
                DateTimeOffset.UtcNow);
        }

        var previousRole = target.Value.Role;
        var now = DateTimeOffset.UtcNow;

        await using (var update = connection.CreateCommand())
        {
            update.Transaction = transaction;
            update.CommandText =
                """
                UPDATE group_members
                SET group_role = $role
                WHERE group_id = $groupId
                  AND user_id = $userId
                  AND removed_utc IS NULL
                  AND group_role <> 'OWNER';
                """;
            update.Parameters.AddWithValue("$role", requestedRole);
            update.Parameters.AddWithValue("$groupId", groupId.ToString("D"));
            update.Parameters.AddWithValue("$userId", targetUserId.ToString("D"));

            if (await update.ExecuteNonQueryAsync(cancellationToken) != 1)
                throw new InvalidOperationException("Group role mutation lost its authority race.");
        }

        var payload =
            $"{{\"previousRole\":\"{previousRole}\",\"role\":\"{requestedRole}\"}}";

        await AppendGroupEventAsync(
            connection,
            transaction,
            groupId,
            actorUserId,
            targetUserId,
            "MEMBER_ROLE_CHANGED",
            payload,
            now,
            cancellationToken);

        await AppendAuditAsync(
            connection,
            transaction,
            actorUserId,
            "GROUP_MEMBER_ROLE_CHANGED",
            groupId,
            payload,
            now,
            cancellationToken);

        transaction.Commit();

        return new GroupMembershipMutationResponse(
            groupId,
            targetUserId,
            requestedRole,
            "ROLE_CHANGED",
            now);
    }

    public async Task<PersistedMessageResult> SendGroupMessageAsync(
        Guid actorUserId,
        Guid groupId,
        SendMessageRequest request,
        CancellationToken cancellationToken = default)
    {
        if (request.ClientMessageId == Guid.Empty)
            throw new ArgumentException("ClientMessageId must be non-empty.", nameof(request));

        var body = MessageTextPolicy.Normalize(
            request.Body,
            MaxMessageLength);

        await using var connection = database.OpenConnection();
        using var transaction = BeginWriteTransaction(connection);

        await RequireActiveGroupMemberAsync(
            connection,
            actorUserId,
            groupId,
            cancellationToken,
            transaction);

        var existing = await FindByClientMessageIdAsync(
            connection,
            transaction,
            actorUserId,
            request.ClientMessageId,
            cancellationToken);

        if (existing is not null)
        {
            EnsureGroupIdempotentReplayMatches(
                existing,
                groupId,
                body,
                request.ReplyToMessageId);
            transaction.Commit();
            return new PersistedMessageResult(existing, false);
        }

        if (request.ReplyToMessageId is { } replyTo)
        {
            await using var reply = connection.CreateCommand();
            reply.Transaction = transaction;
            reply.CommandText =
                """
                SELECT COUNT(1)
                FROM messages
                WHERE message_id = $messageId
                  AND scope_type = 'GROUP'
                  AND scope_id = $groupId
                  AND deleted_utc IS NULL;
                """;
            reply.Parameters.AddWithValue("$messageId", replyTo.ToString("D"));
            reply.Parameters.AddWithValue("$groupId", groupId.ToString("D"));

            if (Convert.ToInt64(
                    await reply.ExecuteScalarAsync(cancellationToken)) != 1)
            {
                throw new ArgumentException(
                    "Reply target is not an active message in this group.",
                    nameof(request));
            }
        }

        var messageId = Guid.NewGuid();
        var now = DateTimeOffset.UtcNow;

        await using var insert = connection.CreateCommand();
        insert.Transaction = transaction;
        insert.CommandText =
            """
            INSERT OR IGNORE INTO messages (
                message_id, scope_type, scope_id, sender_user_id,
                client_message_id, body, reply_to_message_id, created_utc,
                edited_utc, deleted_utc
            ) VALUES (
                $messageId, 'GROUP', $groupId, $senderUserId,
                $clientMessageId, $body, $replyToMessageId, $createdUtc,
                NULL, NULL
            );
            """;
        insert.Parameters.AddWithValue("$messageId", messageId.ToString("D"));
        insert.Parameters.AddWithValue("$groupId", groupId.ToString("D"));
        insert.Parameters.AddWithValue("$senderUserId", actorUserId.ToString("D"));
        insert.Parameters.AddWithValue(
            "$clientMessageId",
            request.ClientMessageId.ToString("D"));
        insert.Parameters.AddWithValue("$body", body);
        insert.Parameters.AddWithValue(
            "$replyToMessageId",
            request.ReplyToMessageId is null
                ? DBNull.Value
                : request.ReplyToMessageId.Value.ToString("D"));
        insert.Parameters.AddWithValue("$createdUtc", now.ToString("O"));

        var inserted = await insert.ExecuteNonQueryAsync(cancellationToken);
        if (inserted == 0)
        {
            var concurrent = await FindByClientMessageIdAsync(
                connection,
                transaction,
                actorUserId,
                request.ClientMessageId,
                cancellationToken)
                ?? throw new InvalidOperationException(
                    "Group message idempotency conflict could not be resolved.");

            EnsureGroupIdempotentReplayMatches(
                concurrent,
                groupId,
                body,
                request.ReplyToMessageId);

            transaction.Commit();
            return new PersistedMessageResult(concurrent, false);
        }

        transaction.Commit();

        return new PersistedMessageResult(
            new MessageResponse(
                messageId,
                "GROUP",
                groupId,
                actorUserId,
                request.ClientMessageId,
                body,
                request.ReplyToMessageId,
                now),
            true);
    }

    public async Task<IReadOnlyList<MessageResponse>> GetGroupHistoryAsync(
        Guid actorUserId,
        Guid groupId,
        Guid? afterMessageId,
        int limit,
        CancellationToken cancellationToken = default)
    {
        if (limit is < 1 or > 250)
            throw new ArgumentOutOfRangeException(nameof(limit));

        await using var connection = database.OpenConnection();
        using var transaction = BeginReadTransaction(connection);

        await RequireActiveGroupMemberAsync(
            connection,
            actorUserId,
            groupId,
            cancellationToken,
            transaction);

        string? afterCreatedUtc = null;
        string? afterId = null;

        if (afterMessageId is { } cursor)
        {
            await using var cursorCommand = connection.CreateCommand();
            cursorCommand.Transaction = transaction;
            cursorCommand.CommandText =
                """
                SELECT created_utc, message_id
                FROM messages
                WHERE message_id = $messageId
                  AND scope_type = 'GROUP'
                  AND scope_id = $groupId;
                """;
            cursorCommand.Parameters.AddWithValue("$messageId", cursor.ToString("D"));
            cursorCommand.Parameters.AddWithValue("$groupId", groupId.ToString("D"));

            await using var cursorReader =
                await cursorCommand.ExecuteReaderAsync(cancellationToken);
            if (!await cursorReader.ReadAsync(cancellationToken))
                throw new KeyNotFoundException(
                    "Message cursor was not found in this group.");

            afterCreatedUtc = cursorReader.GetString(0);
            afterId = cursorReader.GetString(1);
        }

        await using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText =
            """
            SELECT message_id, scope_type, scope_id, sender_user_id,
                   client_message_id, body, reply_to_message_id, created_utc,
                   edited_utc, deleted_utc
            FROM messages
            WHERE scope_type = 'GROUP'
              AND scope_id = $groupId
              AND (
                  $afterCreatedUtc IS NULL
                  OR created_utc > $afterCreatedUtc
                  OR (created_utc = $afterCreatedUtc AND message_id > $afterId)
              )
            ORDER BY created_utc, message_id
            LIMIT $limit;
            """;
        command.Parameters.AddWithValue("$groupId", groupId.ToString("D"));
        command.Parameters.AddWithValue(
            "$afterCreatedUtc",
            afterCreatedUtc is null ? DBNull.Value : afterCreatedUtc);
        command.Parameters.AddWithValue(
            "$afterId",
            afterId is null ? DBNull.Value : afterId);
        command.Parameters.AddWithValue("$limit", limit);

        var messages = new List<MessageResponse>();
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
            messages.Add(ReadMessage(reader));

        transaction.Commit();
        return messages;
    }

    public async Task<RecentMessagePageResponse> GetGroupRecentHistoryPageAsync(
        Guid actorUserId,
        Guid groupId,
        Guid? beforeMessageId = null,
        int limit = 50,
        CancellationToken cancellationToken = default)
    {
        if (limit is < 1 or > MaxMessagePageSize)
            throw new ArgumentOutOfRangeException(nameof(limit));

        await using var connection = database.OpenConnection();
        using var transaction = BeginReadTransaction(connection);

        await RequireActiveGroupMemberAsync(
            connection,
            actorUserId,
            groupId,
            cancellationToken,
            transaction);

        string? beforeCreatedUtc = null;
        string? beforeId = null;

        if (beforeMessageId is { } cursor)
        {
            await using var cursorCommand = connection.CreateCommand();
            cursorCommand.Transaction = transaction;
            cursorCommand.CommandText =
                """
                SELECT created_utc, message_id
                FROM messages
                WHERE message_id = $messageId
                  AND scope_type = 'GROUP'
                  AND scope_id = $groupId;
                """;
            cursorCommand.Parameters.AddWithValue("$messageId", cursor.ToString("D"));
            cursorCommand.Parameters.AddWithValue("$groupId", groupId.ToString("D"));

            await using var cursorReader =
                await cursorCommand.ExecuteReaderAsync(cancellationToken);
            if (!await cursorReader.ReadAsync(cancellationToken))
                throw new KeyNotFoundException(
                    "Recent-history cursor was not found in this group.");

            beforeCreatedUtc = cursorReader.GetString(0);
            beforeId = cursorReader.GetString(1);
        }

        await using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText =
            """
            SELECT message_id, scope_type, scope_id, sender_user_id,
                   client_message_id, body, reply_to_message_id, created_utc,
                   edited_utc, deleted_utc
            FROM messages
            WHERE scope_type = 'GROUP'
              AND scope_id = $groupId
              AND (
                  $beforeCreatedUtc IS NULL
                  OR created_utc < $beforeCreatedUtc
                  OR (created_utc = $beforeCreatedUtc AND message_id < $beforeId)
              )
            ORDER BY created_utc DESC, message_id DESC
            LIMIT $limit;
            """;
        command.Parameters.AddWithValue("$groupId", groupId.ToString("D"));
        command.Parameters.AddWithValue(
            "$beforeCreatedUtc",
            beforeCreatedUtc is null ? DBNull.Value : beforeCreatedUtc);
        command.Parameters.AddWithValue(
            "$beforeId",
            beforeId is null ? DBNull.Value : beforeId);
        command.Parameters.AddWithValue("$limit", limit + 1);

        var descending = new List<MessageResponse>();
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
            descending.Add(ReadMessage(reader));

        var hasOlder = descending.Count > limit;
        if (hasOlder)
            descending.RemoveAt(descending.Count - 1);

        descending.Reverse();

        var page = new RecentMessagePageResponse(
            descending,
            descending.Count == 0 ? null : descending[0].MessageId,
            hasOlder);

        transaction.Commit();
        return page;
    }

    public async Task<MessagePageResponse> GetGroupHistoryPageAsync(
        Guid actorUserId,
        Guid groupId,
        Guid? afterMessageId,
        int limit,
        CancellationToken cancellationToken = default)
    {
        if (limit is < 1 or > MaxMessagePageSize)
            throw new ArgumentOutOfRangeException(nameof(limit));

        var items = await GetGroupHistoryAsync(
            actorUserId,
            groupId,
            afterMessageId,
            limit + 1,
            cancellationToken);

        var hasMore = items.Count > limit;
        var pageItems = hasMore
            ? items.Take(limit).ToArray()
            : items.ToArray();

        return new MessagePageResponse(
            pageItems,
            pageItems.Length == 0 ? null : pageItems[^1].MessageId,
            hasMore);
    }

    public async Task<IReadOnlyList<Guid>> GetActiveGroupMemberIdsAsync(
        Guid actorUserId,
        Guid groupId,
        CancellationToken cancellationToken = default)
    {
        await using var connection = database.OpenConnection();
        await RequireActiveGroupMemberAsync(
            connection,
            actorUserId,
            groupId,
            cancellationToken);

        return await ListActiveGroupMemberIdsAsync(
            connection,
            transaction: null,
            groupId,
            cancellationToken);
    }

    /// <summary>
    /// Server-internal post-authorization projection used only after a group
    /// mutation has already established caller authority. Do not expose this
    /// method directly as a client authorization surface.
    /// </summary>
    public async Task<IReadOnlyList<Guid>> GetGroupDeliveryTargetUserIdsAsync(
        Guid groupId,
        CancellationToken cancellationToken = default)
    {
        await using var connection = database.OpenConnection();
        return await ListActiveGroupMemberIdsAsync(
            connection,
            transaction: null,
            groupId,
            cancellationToken);
    }

    public async Task<IReadOnlyList<MessageReceiptResponse>> MarkGroupMessageDeliveredAsync(
        Guid actorUserId,
        Guid groupId,
        Guid messageId,
        CancellationToken cancellationToken = default)
    {
        await using var connection = database.OpenConnection();
        using var transaction = BeginWriteTransaction(connection);

        await RequireActiveGroupMemberAsync(
            connection,
            actorUserId,
            groupId,
            cancellationToken,
            transaction);

        var senderUserId = await RequireGroupMessageAsync(
            connection,
            groupId,
            messageId,
            cancellationToken,
            transaction);

        if (senderUserId == actorUserId)
            throw new InvalidOperationException(
                "A sender cannot acknowledge their own group message as delivered.");

        var now = DateTimeOffset.UtcNow.ToString("O");

        await using (var receipt = connection.CreateCommand())
        {
            receipt.Transaction = transaction;
            receipt.CommandText =
                """
                INSERT INTO message_receipts (
                    message_id, user_id, delivered_utc, read_utc
                ) VALUES (
                    $messageId, $userId, $utc, NULL
                )
                ON CONFLICT(message_id, user_id) DO UPDATE SET
                    delivered_utc = COALESCE(
                        message_receipts.delivered_utc,
                        excluded.delivered_utc);
                """;
            receipt.Parameters.AddWithValue("$messageId", messageId.ToString("D"));
            receipt.Parameters.AddWithValue("$userId", actorUserId.ToString("D"));
            receipt.Parameters.AddWithValue("$utc", now);
            await receipt.ExecuteNonQueryAsync(cancellationToken);
        }

        var receipts = await ListGroupMessageReceiptsAsync(
            connection,
            transaction,
            messageId,
            cancellationToken);

        transaction.Commit();
        return receipts;
    }

    public async Task<IReadOnlyList<MessageReceiptResponse>> MarkGroupMessageReadAsync(
        Guid actorUserId,
        Guid groupId,
        Guid messageId,
        CancellationToken cancellationToken = default)
    {
        await using var connection = database.OpenConnection();
        using var transaction = BeginWriteTransaction(connection);

        await RequireActiveGroupMemberAsync(
            connection,
            actorUserId,
            groupId,
            cancellationToken,
            transaction);

        var senderUserId = await RequireGroupMessageAsync(
            connection,
            groupId,
            messageId,
            cancellationToken,
            transaction);

        if (senderUserId == actorUserId)
            throw new InvalidOperationException(
                "A sender cannot acknowledge their own group message as read.");

        var now = DateTimeOffset.UtcNow.ToString("O");

        await using (var receipt = connection.CreateCommand())
        {
            receipt.Transaction = transaction;
            receipt.CommandText =
                """
                INSERT INTO message_receipts (
                    message_id, user_id, delivered_utc, read_utc
                ) VALUES (
                    $messageId, $userId, $utc, $utc
                )
                ON CONFLICT(message_id, user_id) DO UPDATE SET
                    delivered_utc = COALESCE(
                        message_receipts.delivered_utc,
                        excluded.delivered_utc),
                    read_utc = COALESCE(
                        message_receipts.read_utc,
                        excluded.read_utc);
                """;
            receipt.Parameters.AddWithValue("$messageId", messageId.ToString("D"));
            receipt.Parameters.AddWithValue("$userId", actorUserId.ToString("D"));
            receipt.Parameters.AddWithValue("$utc", now);
            await receipt.ExecuteNonQueryAsync(cancellationToken);
        }

        var receipts = await ListGroupMessageReceiptsAsync(
            connection,
            transaction,
            messageId,
            cancellationToken);

        transaction.Commit();
        return receipts;
    }

    public async Task<IReadOnlyList<MessageReceiptResponse>> GetGroupMessageReceiptsAsync(
        Guid actorUserId,
        Guid groupId,
        Guid messageId,
        CancellationToken cancellationToken = default)
    {
        await using var connection = database.OpenConnection();
        await RequireActiveGroupMemberAsync(
            connection,
            actorUserId,
            groupId,
            cancellationToken);

        await RequireGroupMessageAsync(
            connection,
            groupId,
            messageId,
            cancellationToken);

        return await ListGroupMessageReceiptsAsync(
            connection,
            transaction: null,
            messageId,
            cancellationToken);
    }

    public async Task<MessageResponse> GetGroupMessageByIdAsync(
        Guid actorUserId,
        Guid groupId,
        Guid messageId,
        CancellationToken cancellationToken = default)
    {
        await using var connection = database.OpenConnection();
        await RequireActiveGroupMemberAsync(
            connection,
            actorUserId,
            groupId,
            cancellationToken);

        return await GetGroupMessageAsync(
            connection,
            transaction: null,
            groupId,
            messageId,
            cancellationToken)
            ?? throw new KeyNotFoundException("Group message not found.");
    }

    public async Task<MessageResponse> EditGroupMessageAsync(
        Guid actorUserId,
        Guid groupId,
        Guid messageId,
        EditMessageRequest request,
        CancellationToken cancellationToken = default)
    {
        var body = MessageTextPolicy.Normalize(
            request.Body,
            MaxMessageLength);

        await using var connection = database.OpenConnection();
        using var transaction = BeginWriteTransaction(connection);

        await RequireActiveGroupMemberAsync(
            connection,
            actorUserId,
            groupId,
            cancellationToken,
            transaction);

        var existing = await GetGroupMessageAsync(
            connection,
            transaction,
            groupId,
            messageId,
            cancellationToken)
            ?? throw new KeyNotFoundException("Group message not found.");

        if (existing.DeletedUtc is not null)
            throw new InvalidOperationException("Deleted group messages cannot be edited.");

        if (existing.SenderUserId != actorUserId)
            throw new UnauthorizedAccessException(
                "Only the sender can edit this group message.");

        var editedUtc = DateTimeOffset.UtcNow;

        await using (var update = connection.CreateCommand())
        {
            update.Transaction = transaction;
            update.CommandText =
                """
                UPDATE messages
                SET body = $body,
                    edited_utc = $editedUtc
                WHERE message_id = $messageId
                  AND scope_type = 'GROUP'
                  AND scope_id = $groupId
                  AND deleted_utc IS NULL;
                """;
            update.Parameters.AddWithValue("$body", body);
            update.Parameters.AddWithValue("$editedUtc", editedUtc.ToString("O"));
            update.Parameters.AddWithValue("$messageId", messageId.ToString("D"));
            update.Parameters.AddWithValue("$groupId", groupId.ToString("D"));

            if (await update.ExecuteNonQueryAsync(cancellationToken) != 1)
                throw new InvalidOperationException(
                    "Group message edit could not be persisted.");
        }

        var edited = await GetGroupMessageAsync(
            connection,
            transaction,
            groupId,
            messageId,
            cancellationToken)
            ?? throw new InvalidOperationException(
                "Edited group message could not be reloaded.");

        transaction.Commit();
        return edited;
    }

    public async Task<GroupMessageDeletedResponse> DeleteGroupMessageAsync(
        Guid actorUserId,
        Guid groupId,
        Guid messageId,
        CancellationToken cancellationToken = default)
    {
        await using var connection = database.OpenConnection();
        using var transaction = BeginWriteTransaction(connection);

        await RequireActiveGroupMemberAsync(
            connection,
            actorUserId,
            groupId,
            cancellationToken,
            transaction);

        var existing = await GetGroupMessageAsync(
            connection,
            transaction,
            groupId,
            messageId,
            cancellationToken)
            ?? throw new KeyNotFoundException("Group message not found.");

        if (existing.SenderUserId != actorUserId)
            throw new UnauthorizedAccessException(
                "Only the sender can delete this group message.");

        if (existing.DeletedUtc is { } alreadyDeleted)
        {
            transaction.Commit();
            return new GroupMessageDeletedResponse(
                messageId,
                groupId,
                alreadyDeleted);
        }

        var deletedUtc = DateTimeOffset.UtcNow;

        await using (var update = connection.CreateCommand())
        {
            update.Transaction = transaction;
            update.CommandText =
                """
                UPDATE messages
                SET body = NULL,
                    deleted_utc = $deletedUtc
                WHERE message_id = $messageId
                  AND scope_type = 'GROUP'
                  AND scope_id = $groupId
                  AND deleted_utc IS NULL;
                """;
            update.Parameters.AddWithValue("$deletedUtc", deletedUtc.ToString("O"));
            update.Parameters.AddWithValue("$messageId", messageId.ToString("D"));
            update.Parameters.AddWithValue("$groupId", groupId.ToString("D"));

            if (await update.ExecuteNonQueryAsync(cancellationToken) != 1)
                throw new InvalidOperationException(
                    "Group message delete could not be persisted.");
        }

        transaction.Commit();
        return new GroupMessageDeletedResponse(
            messageId,
            groupId,
            deletedUtc);
    }

    public async Task<IReadOnlyList<GroupEventResponse>> ListGroupEventsAsync(
        Guid actorUserId,
        Guid groupId,
        Guid? afterEventId = null,
        int limit = 100,
        CancellationToken cancellationToken = default)
    {
        if (limit is < 1 or > 250)
            throw new ArgumentOutOfRangeException(nameof(limit));

        await using var connection = database.OpenConnection();
        await RequireActiveGroupMemberAsync(
            connection,
            actorUserId,
            groupId,
            cancellationToken);

        string? afterCreatedUtc = null;
        string? afterId = null;

        if (afterEventId is { } cursor)
        {
            await using var cursorCommand = connection.CreateCommand();
            cursorCommand.CommandText =
                """
                SELECT created_utc, group_event_id
                FROM group_events
                WHERE group_event_id = $eventId
                  AND group_id = $groupId;
                """;
            cursorCommand.Parameters.AddWithValue("$eventId", cursor.ToString("D"));
            cursorCommand.Parameters.AddWithValue("$groupId", groupId.ToString("D"));

            await using var reader =
                await cursorCommand.ExecuteReaderAsync(cancellationToken);
            if (!await reader.ReadAsync(cancellationToken))
                throw new KeyNotFoundException(
                    "Group event cursor was not found.");

            afterCreatedUtc = reader.GetString(0);
            afterId = reader.GetString(1);
        }

        await using var command = connection.CreateCommand();
        command.CommandText =
            """
            SELECT group_event_id, group_id, actor_user_id,
                   subject_user_id, event_type, payload_json, created_utc
            FROM group_events
            WHERE group_id = $groupId
              AND (
                  $afterCreatedUtc IS NULL
                  OR created_utc > $afterCreatedUtc
                  OR (created_utc = $afterCreatedUtc AND group_event_id > $afterId)
              )
            ORDER BY created_utc, group_event_id
            LIMIT $limit;
            """;
        command.Parameters.AddWithValue("$groupId", groupId.ToString("D"));
        command.Parameters.AddWithValue(
            "$afterCreatedUtc",
            afterCreatedUtc is null ? DBNull.Value : afterCreatedUtc);
        command.Parameters.AddWithValue(
            "$afterId",
            afterId is null ? DBNull.Value : afterId);
        command.Parameters.AddWithValue("$limit", limit);

        var events = new List<GroupEventResponse>();
        await using var eventReader =
            await command.ExecuteReaderAsync(cancellationToken);

        while (await eventReader.ReadAsync(cancellationToken))
        {
            events.Add(new GroupEventResponse(
                Guid.Parse(eventReader.GetString(0)),
                Guid.Parse(eventReader.GetString(1)),
                eventReader.IsDBNull(2)
                    ? null
                    : Guid.Parse(eventReader.GetString(2)),
                eventReader.IsDBNull(3)
                    ? null
                    : Guid.Parse(eventReader.GetString(3)),
                eventReader.GetString(4),
                eventReader.GetString(5),
                DateTimeOffset.Parse(eventReader.GetString(6))));
        }

        return events;
    }

    private static string NormalizeAssignableGroupRole(string role)
    {
        if (string.IsNullOrWhiteSpace(role))
            throw new ArgumentException("Group role is required.", nameof(role));

        var normalized = role.Trim().ToUpperInvariant();
        return normalized is "ADMIN" or "MEMBER"
            ? normalized
            : throw new ArgumentException(
                "Assignable group role must be ADMIN or MEMBER.",
                nameof(role));
    }

    private static async Task<(string Role, DateTimeOffset? RemovedUtc)?> GetGroupMemberStateAsync(
        SqliteConnection connection,
        Guid groupId,
        Guid userId,
        CancellationToken cancellationToken,
        SqliteTransaction? transaction = null)
    {
        await using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText =
            """
            SELECT group_role, removed_utc
            FROM group_members
            WHERE group_id = $groupId
              AND user_id = $userId;
            """;
        command.Parameters.AddWithValue("$groupId", groupId.ToString("D"));
        command.Parameters.AddWithValue("$userId", userId.ToString("D"));

        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        if (!await reader.ReadAsync(cancellationToken))
            return null;

        return (
            reader.GetString(0),
            reader.IsDBNull(1)
                ? null
                : DateTimeOffset.Parse(reader.GetString(1)));
    }

    private static async Task<MessageResponse?> GetGroupMessageAsync(
        SqliteConnection connection,
        SqliteTransaction? transaction,
        Guid groupId,
        Guid messageId,
        CancellationToken cancellationToken)
    {
        await using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText =
            """
            SELECT message_id, scope_type, scope_id, sender_user_id,
                   client_message_id, body, reply_to_message_id, created_utc,
                   edited_utc, deleted_utc
            FROM messages
            WHERE message_id = $messageId
              AND scope_type = 'GROUP'
              AND scope_id = $groupId;
            """;
        command.Parameters.AddWithValue("$messageId", messageId.ToString("D"));
        command.Parameters.AddWithValue("$groupId", groupId.ToString("D"));

        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        return await reader.ReadAsync(cancellationToken)
            ? ReadMessage(reader)
            : null;
    }

    private static async Task<IReadOnlyList<Guid>> ListActiveGroupMemberIdsAsync(
        SqliteConnection connection,
        SqliteTransaction? transaction,
        Guid groupId,
        CancellationToken cancellationToken)
    {
        await using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText =
            """
            SELECT gm.user_id
            FROM group_members gm
            JOIN users u ON u.user_id = gm.user_id
            WHERE gm.group_id = $groupId
              AND gm.removed_utc IS NULL
              AND u.disabled_utc IS NULL
            ORDER BY gm.user_id;
            """;
        command.Parameters.AddWithValue("$groupId", groupId.ToString("D"));

        var members = new List<Guid>();
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
            members.Add(Guid.Parse(reader.GetString(0)));

        return members;
    }

    private static async Task<IReadOnlyList<MessageReceiptResponse>> ListGroupMessageReceiptsAsync(
        SqliteConnection connection,
        SqliteTransaction? transaction,
        Guid messageId,
        CancellationToken cancellationToken)
    {
        await using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText =
            """
            SELECT message_id, user_id, delivered_utc, read_utc
            FROM message_receipts
            WHERE message_id = $messageId
            ORDER BY user_id;
            """;
        command.Parameters.AddWithValue("$messageId", messageId.ToString("D"));

        var receipts = new List<MessageReceiptResponse>();
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            receipts.Add(new MessageReceiptResponse(
                Guid.Parse(reader.GetString(0)),
                Guid.Parse(reader.GetString(1)),
                reader.IsDBNull(2)
                    ? null
                    : DateTimeOffset.Parse(reader.GetString(2)),
                reader.IsDBNull(3)
                    ? null
                    : DateTimeOffset.Parse(reader.GetString(3))));
        }

        return receipts;
    }

    private static async Task<Guid> RequireGroupMessageAsync(
        SqliteConnection connection,
        Guid groupId,
        Guid messageId,
        CancellationToken cancellationToken,
        SqliteTransaction? transaction = null)
    {
        await using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText =
            """
            SELECT sender_user_id
            FROM messages
            WHERE message_id = $messageId
              AND scope_type = 'GROUP'
              AND scope_id = $groupId
              AND deleted_utc IS NULL;
            """;
        command.Parameters.AddWithValue("$messageId", messageId.ToString("D"));
        command.Parameters.AddWithValue("$groupId", groupId.ToString("D"));

        var sender = Convert.ToString(
            await command.ExecuteScalarAsync(cancellationToken));

        return !string.IsNullOrWhiteSpace(sender)
            ? Guid.Parse(sender)
            : throw new KeyNotFoundException("Active group message was not found.");
    }

    private static async Task<MessageResponse?> FindByClientMessageIdAsync(
        SqliteConnection connection,
        SqliteTransaction transaction,
        Guid senderUserId,
        Guid clientMessageId,
        CancellationToken cancellationToken)
    {
        await using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText =
            """
            SELECT message_id, scope_type, scope_id, sender_user_id,
                   client_message_id, body, reply_to_message_id, created_utc,
                   edited_utc, deleted_utc
            FROM messages
            WHERE sender_user_id = $senderUserId
              AND client_message_id = $clientMessageId;
            """;
        command.Parameters.AddWithValue("$senderUserId", senderUserId.ToString("D"));
        command.Parameters.AddWithValue(
            "$clientMessageId",
            clientMessageId.ToString("D"));

        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        return await reader.ReadAsync(cancellationToken)
            ? ReadMessage(reader)
            : null;
    }

    private static void EnsureGroupIdempotentReplayMatches(
        MessageResponse existing,
        Guid groupId,
        string body,
        Guid? replyToMessageId)
    {
        if (existing.ScopeType != "GROUP" ||
            existing.ScopeId != groupId ||
            !string.Equals(existing.Body, body, StringComparison.Ordinal) ||
            existing.ReplyToMessageId != replyToMessageId)
        {
            throw new InvalidOperationException(
                "ClientMessageId was already used with different message content.");
        }
    }

    private static MessageResponse ReadMessage(SqliteDataReader reader) =>
        new(
            Guid.Parse(reader.GetString(0)),
            reader.GetString(1),
            Guid.Parse(reader.GetString(2)),
            Guid.Parse(reader.GetString(3)),
            Guid.Parse(reader.GetString(4)),
            reader.IsDBNull(5) ? string.Empty : reader.GetString(5),
            reader.IsDBNull(6) ? null : Guid.Parse(reader.GetString(6)),
            DateTimeOffset.Parse(reader.GetString(7)),
            reader.IsDBNull(8) ? null : DateTimeOffset.Parse(reader.GetString(8)),
            reader.IsDBNull(9) ? null : DateTimeOffset.Parse(reader.GetString(9)));

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
        CancellationToken cancellationToken,
        SqliteTransaction? transaction = null)
    {
        await using var command = connection.CreateCommand();
        command.Transaction = transaction;
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
