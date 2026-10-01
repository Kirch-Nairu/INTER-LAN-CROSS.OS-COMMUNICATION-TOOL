using System.Text.Json;
using InterLan.Domain.Crm;
using Microsoft.Data.Sqlite;

namespace InterLan.Infrastructure.Crm;

public sealed partial class CrmAuthorityStore
{
    private readonly SqliteDatabase database;

    public CrmAuthorityStore(SqliteDatabase database)
    {
        this.database = database ?? throw new ArgumentNullException(nameof(database));
    }

    public async Task CreateProjectAsync(
        Guid projectId,
        string projectKey,
        string name,
        Guid actorUserId,
        DateTimeOffset now,
        CancellationToken cancellationToken = default)
    {
        if (projectId == Guid.Empty || actorUserId == Guid.Empty)
            throw new ArgumentException("Project and actor IDs cannot be empty.");
        var normalizedKey = CrmRequirement.NormalizeKey(projectKey, nameof(projectKey));
        var normalizedName = CrmRequirement.NormalizeText(name, nameof(name));

        await using var connection = database.OpenConnection();
        using var transaction = connection.BeginTransaction();

        await using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText =
            """
            INSERT INTO crm_projects (
                project_id, project_key, name, created_by_user_id, created_utc)
            VALUES ($id, $key, $name, $actor, $utc);
            """;
        command.Parameters.AddWithValue("$id", projectId.ToString("D"));
        command.Parameters.AddWithValue("$key", normalizedKey);
        command.Parameters.AddWithValue("$name", normalizedName);
        command.Parameters.AddWithValue("$actor", actorUserId.ToString("D"));
        command.Parameters.AddWithValue("$utc", now.ToString("O"));
        await command.ExecuteNonQueryAsync(cancellationToken);

        await AppendAuditAsync(
            connection,
            transaction,
            actorUserId,
            null,
            null,
            "CRM_PROJECT_CREATED",
            null,
            null,
            new { projectId, projectKey = normalizedKey, name = normalizedName },
            now,
            cancellationToken);

        transaction.Commit();
    }

    public async Task CreateFeatureAsync(
        Guid featureId,
        Guid projectId,
        string featureKey,
        string name,
        Guid actorUserId,
        DateTimeOffset now,
        CancellationToken cancellationToken = default)
    {
        if (featureId == Guid.Empty || projectId == Guid.Empty || actorUserId == Guid.Empty)
            throw new ArgumentException("Feature, project and actor IDs cannot be empty.");
        var normalizedKey = CrmRequirement.NormalizeKey(featureKey, nameof(featureKey));
        var normalizedName = CrmRequirement.NormalizeText(name, nameof(name));

        await using var connection = database.OpenConnection();
        using var transaction = connection.BeginTransaction();

        await using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText =
            """
            INSERT INTO crm_features (
                feature_id, project_id, feature_key, name, created_by_user_id, created_utc)
            VALUES ($id, $project, $key, $name, $actor, $utc);
            """;
        command.Parameters.AddWithValue("$id", featureId.ToString("D"));
        command.Parameters.AddWithValue("$project", projectId.ToString("D"));
        command.Parameters.AddWithValue("$key", normalizedKey);
        command.Parameters.AddWithValue("$name", normalizedName);
        command.Parameters.AddWithValue("$actor", actorUserId.ToString("D"));
        command.Parameters.AddWithValue("$utc", now.ToString("O"));
        await command.ExecuteNonQueryAsync(cancellationToken);

        await AppendAuditAsync(
            connection,
            transaction,
            actorUserId,
            null,
            null,
            "CRM_FEATURE_CREATED",
            null,
            null,
            new { featureId, projectId, featureKey = normalizedKey, name = normalizedName },
            now,
            cancellationToken);

        transaction.Commit();
    }

    public async Task GrantRoleAsync(
        Guid userId,
        CrmStaffRole role,
        Guid actorUserId,
        DateTimeOffset now,
        CancellationToken cancellationToken = default)
    {
        if (userId == Guid.Empty || actorUserId == Guid.Empty)
            throw new ArgumentException("User IDs cannot be empty.");

        await using var connection = database.OpenConnection();
        using var transaction = connection.BeginTransaction();

        await using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText =
            """
            INSERT INTO crm_staff_role_grants (
                role_grant_id, user_id, crm_role, granted_by_user_id, granted_utc, revoked_utc)
            VALUES ($id, $user, $role, $actor, $utc, NULL);
            """;
        command.Parameters.AddWithValue("$id", Guid.NewGuid().ToString("D"));
        command.Parameters.AddWithValue("$user", userId.ToString("D"));
        command.Parameters.AddWithValue("$role", CrmCapabilityPolicy.Serialize(role));
        command.Parameters.AddWithValue("$actor", actorUserId.ToString("D"));
        command.Parameters.AddWithValue("$utc", now.ToString("O"));
        await command.ExecuteNonQueryAsync(cancellationToken);

        await AppendAuditAsync(
            connection,
            transaction,
            actorUserId,
            null,
            null,
            "CRM_ROLE_GRANTED",
            null,
            null,
            new { userId, role = CrmCapabilityPolicy.Serialize(role) },
            now,
            cancellationToken);

        transaction.Commit();
    }

    public async Task<IReadOnlyList<CrmStaffRole>> GetActiveRolesAsync(
        Guid userId,
        CancellationToken cancellationToken = default)
    {
        await using var connection = database.OpenConnection();
        await using var command = connection.CreateCommand();
        command.CommandText =
            """
            SELECT crm_role
            FROM crm_staff_role_grants
            WHERE user_id = $user AND revoked_utc IS NULL
            ORDER BY crm_role;
            """;
        command.Parameters.AddWithValue("$user", userId.ToString("D"));

        var roles = new List<CrmStaffRole>();
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
            roles.Add(CrmCapabilityPolicy.ParseRole(reader.GetString(0)));

        return roles;
    }

    public async Task<Guid?> ResolveActiveUserAsync(
        string identity,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(identity);

        await using var connection = database.OpenConnection();
        await using var command = connection.CreateCommand();
        command.CommandText =
            """
            SELECT user_id
            FROM users
            WHERE disabled_utc IS NULL
              AND (username = $identity COLLATE NOCASE OR display_name = $identity COLLATE NOCASE)
            ORDER BY CASE WHEN username = $identity COLLATE NOCASE THEN 0 ELSE 1 END
            LIMIT 1;
            """;
        command.Parameters.AddWithValue("$identity", identity.Trim());
        var value = await command.ExecuteScalarAsync(cancellationToken);
        return value is null || value is DBNull ? null : Guid.Parse(Convert.ToString(value)!);
    }

    private static async Task AppendAuditAsync(
        SqliteConnection connection,
        SqliteTransaction transaction,
        Guid actorUserId,
        CrmWorkPackageId? workPackageId,
        Guid? candidateId,
        string eventType,
        CrmWorkPackageState? beforeState,
        CrmWorkPackageState? afterState,
        object payload,
        DateTimeOffset now,
        CancellationToken cancellationToken)
    {
        await using var audit = connection.CreateCommand();
        audit.Transaction = transaction;
        audit.CommandText =
            """
            INSERT INTO crm_audit_events (
                audit_event_id, actor_user_id, work_package_id, candidate_id,
                event_type, before_state, after_state, payload_json, created_utc)
            VALUES ($id, $actor, $workPackage, $candidate,
                $eventType, $beforeState, $afterState, $payload, $utc);
            """;
        audit.Parameters.AddWithValue("$id", Guid.NewGuid().ToString("D"));
        audit.Parameters.AddWithValue("$actor", actorUserId.ToString("D"));
        audit.Parameters.AddWithValue(
            "$workPackage",
            workPackageId is { } packageId ? packageId.Value : DBNull.Value);
        audit.Parameters.AddWithValue(
            "$candidate",
            candidateId is { } candidate ? candidate.ToString("D") : DBNull.Value);
        audit.Parameters.AddWithValue("$eventType", eventType);
        audit.Parameters.AddWithValue(
            "$beforeState",
            beforeState is { } before ? CrmWorkflowVocabulary.Serialize(before) : DBNull.Value);
        audit.Parameters.AddWithValue(
            "$afterState",
            afterState is { } after ? CrmWorkflowVocabulary.Serialize(after) : DBNull.Value);
        audit.Parameters.AddWithValue("$payload", JsonSerializer.Serialize(payload));
        audit.Parameters.AddWithValue("$utc", now.ToString("O"));
        await audit.ExecuteNonQueryAsync(cancellationToken);
    }
}
