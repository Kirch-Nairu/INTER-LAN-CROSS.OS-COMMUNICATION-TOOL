using System.Text.Json;
using InterLan.Domain.Crm;
using Microsoft.Data.Sqlite;

namespace InterLan.Infrastructure.Crm;

public sealed partial class CrmAuthorityStore
{
    public Task AddRequirementAsync(CrmWorkPackageId id, CrmRequirement requirement, Guid actorUserId, DateTimeOffset now, CancellationToken cancellationToken = default) =>
        InsertDefinitionAsync(id, "crm_requirements", "requirement_id", requirement.Id, "requirement_text", requirement.Text, actorUserId, "CRM_REQUIREMENT_ADDED", now, cancellationToken);

    public Task AddDeliverableAsync(CrmWorkPackageId id, CrmDeliverable deliverable, Guid actorUserId, DateTimeOffset now, CancellationToken cancellationToken = default) =>
        InsertDefinitionAsync(id, "crm_deliverables", "deliverable_id", deliverable.Id, "deliverable_text", deliverable.Text, actorUserId, "CRM_DELIVERABLE_ADDED", now, cancellationToken);

    public async Task AddAcceptanceCriterionAsync(
        CrmWorkPackageId id,
        CrmAcceptanceCriterion criterion,
        Guid actorUserId,
        DateTimeOffset now,
        CancellationToken cancellationToken = default)
    {
        await using var connection = database.OpenConnection();
        using var transaction = connection.BeginTransaction();
        await RequireStateAsync(connection, transaction, id, CrmWorkPackageState.Draft, cancellationToken);

        await using var ordinal = connection.CreateCommand();
        ordinal.Transaction = transaction;
        ordinal.CommandText = "SELECT COUNT(1) FROM crm_acceptance_criteria WHERE work_package_id = $id;";
        ordinal.Parameters.AddWithValue("$id", id.Value);
        var nextOrdinal = Convert.ToInt32(await ordinal.ExecuteScalarAsync(cancellationToken));

        await using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText =
            """
            INSERT INTO crm_acceptance_criteria (
                work_package_id, criterion_id, criterion_text, evidence_kinds_json, ordinal)
            VALUES ($workPackage, $criterion, $text, $evidenceKinds, $ordinal);
            """;
        command.Parameters.AddWithValue("$workPackage", id.Value);
        command.Parameters.AddWithValue("$criterion", criterion.Id);
        command.Parameters.AddWithValue("$text", criterion.Text);
        command.Parameters.AddWithValue("$evidenceKinds", JsonSerializer.Serialize(criterion.EvidenceKinds));
        command.Parameters.AddWithValue("$ordinal", nextOrdinal);
        await command.ExecuteNonQueryAsync(cancellationToken);
        await TouchAsync(connection, transaction, id, now, cancellationToken);
        await AppendAuditAsync(connection, transaction, actorUserId, id, null, "CRM_ACCEPTANCE_CRITERION_ADDED", CrmWorkPackageState.Draft, CrmWorkPackageState.Draft, new { criterion.Id }, now, cancellationToken);
        transaction.Commit();
    }

    public async Task AddScopeRuleAsync(
        CrmWorkPackageId id,
        CrmScopeRule rule,
        Guid actorUserId,
        DateTimeOffset now,
        CancellationToken cancellationToken = default)
    {
        await using var connection = database.OpenConnection();
        using var transaction = connection.BeginTransaction();
        await RequireStateAsync(connection, transaction, id, CrmWorkPackageState.Draft, cancellationToken);

        var opposite = rule.Kind == CrmScopeKind.Owned ? "PROHIBITED" : "OWNED";
        await using var conflict = connection.CreateCommand();
        conflict.Transaction = transaction;
        conflict.CommandText =
            "SELECT COUNT(1) FROM crm_scope_rules WHERE work_package_id = $id AND scope_kind = $kind AND pattern = $pattern;";
        conflict.Parameters.AddWithValue("$id", id.Value);
        conflict.Parameters.AddWithValue("$kind", opposite);
        conflict.Parameters.AddWithValue("$pattern", rule.Pattern);
        if (Convert.ToInt64(await conflict.ExecuteScalarAsync(cancellationToken)) > 0)
            throw new InvalidOperationException($"Scope pattern '{rule.Pattern}' cannot be both owned and prohibited.");

        await using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText =
            """
            INSERT OR IGNORE INTO crm_scope_rules (
                scope_rule_id, work_package_id, scope_kind, pattern)
            VALUES ($ruleId, $workPackage, $kind, $pattern);
            """;
        command.Parameters.AddWithValue("$ruleId", Guid.NewGuid().ToString("D"));
        command.Parameters.AddWithValue("$workPackage", id.Value);
        command.Parameters.AddWithValue("$kind", rule.Kind == CrmScopeKind.Owned ? "OWNED" : "PROHIBITED");
        command.Parameters.AddWithValue("$pattern", rule.Pattern);
        var inserted = await command.ExecuteNonQueryAsync(cancellationToken);
        if (inserted > 0)
        {
            await TouchAsync(connection, transaction, id, now, cancellationToken);
            await AppendAuditAsync(connection, transaction, actorUserId, id, null, "CRM_SCOPE_RULE_ADDED", CrmWorkPackageState.Draft, CrmWorkPackageState.Draft, new { kind = rule.Kind.ToString(), rule.Pattern }, now, cancellationToken);
        }
        transaction.Commit();
    }

    public async Task AssignAsync(
        CrmWorkPackageId id,
        Guid userId,
        CrmStaffRole role,
        Guid actorUserId,
        DateTimeOffset now,
        CancellationToken cancellationToken = default)
    {
        if (userId == Guid.Empty || actorUserId == Guid.Empty)
            throw new ArgumentException("Assignment identities cannot be empty.");

        await using var connection = database.OpenConnection();
        using var transaction = connection.BeginTransaction();
        var snapshot = await ReadSnapshotAsync(connection, transaction, id, cancellationToken);
        if (snapshot.State is not (CrmWorkPackageState.Draft or CrmWorkPackageState.Authorized))
            throw new InvalidOperationException("Assignments can only change before implementation begins.");
        CrmAssignmentPolicy.RequireCompatible(role, snapshot.Layer);

        await using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText =
            """
            INSERT OR IGNORE INTO crm_assignments (
                assignment_id, work_package_id, user_id, crm_role, assigned_by_user_id, assigned_utc, revoked_utc)
            VALUES ($assignmentId, $workPackage, $user, $role, $actor, $utc, NULL);
            """;
        command.Parameters.AddWithValue("$assignmentId", Guid.NewGuid().ToString("D"));
        command.Parameters.AddWithValue("$workPackage", id.Value);
        command.Parameters.AddWithValue("$user", userId.ToString("D"));
        command.Parameters.AddWithValue("$role", CrmCapabilityPolicy.Serialize(role));
        command.Parameters.AddWithValue("$actor", actorUserId.ToString("D"));
        command.Parameters.AddWithValue("$utc", now.ToString("O"));
        var inserted = await command.ExecuteNonQueryAsync(cancellationToken);
        if (inserted > 0)
        {
            await TouchAsync(connection, transaction, id, now, cancellationToken);
            await AppendAuditAsync(connection, transaction, actorUserId, id, null, "CRM_ASSIGNMENT_ADDED", snapshot.State, snapshot.State, new { userId, role = CrmCapabilityPolicy.Serialize(role) }, now, cancellationToken);
        }
        transaction.Commit();
    }

    private async Task InsertDefinitionAsync(
        CrmWorkPackageId id,
        string table,
        string keyColumn,
        string key,
        string textColumn,
        string text,
        Guid actorUserId,
        string eventType,
        DateTimeOffset now,
        CancellationToken cancellationToken)
    {
        await using var connection = database.OpenConnection();
        using var transaction = connection.BeginTransaction();
        await RequireStateAsync(connection, transaction, id, CrmWorkPackageState.Draft, cancellationToken);

        await using var ordinal = connection.CreateCommand();
        ordinal.Transaction = transaction;
        ordinal.CommandText = $"SELECT COUNT(1) FROM {table} WHERE work_package_id = $id;";
        ordinal.Parameters.AddWithValue("$id", id.Value);
        var nextOrdinal = Convert.ToInt32(await ordinal.ExecuteScalarAsync(cancellationToken));

        await using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = $"INSERT INTO {table} (work_package_id, {keyColumn}, {textColumn}, ordinal) VALUES ($id, $key, $text, $ordinal);";
        command.Parameters.AddWithValue("$id", id.Value);
        command.Parameters.AddWithValue("$key", key);
        command.Parameters.AddWithValue("$text", text);
        command.Parameters.AddWithValue("$ordinal", nextOrdinal);
        await command.ExecuteNonQueryAsync(cancellationToken);
        await TouchAsync(connection, transaction, id, now, cancellationToken);
        await AppendAuditAsync(connection, transaction, actorUserId, id, null, eventType, CrmWorkPackageState.Draft, CrmWorkPackageState.Draft, new { key }, now, cancellationToken);
        transaction.Commit();
    }

    private static async Task RequireStateAsync(SqliteConnection connection, SqliteTransaction transaction, CrmWorkPackageId id, CrmWorkPackageState expected, CancellationToken cancellationToken)
    {
        await using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = "SELECT state FROM crm_work_packages WHERE work_package_id = $id;";
        command.Parameters.AddWithValue("$id", id.Value);
        var state = Convert.ToString(await command.ExecuteScalarAsync(cancellationToken))
            ?? throw new KeyNotFoundException($"CRM work package {id} was not found.");
        if (CrmWorkflowVocabulary.ParseState(state) != expected)
            throw new InvalidOperationException($"CRM work package {id} must be {expected} for this operation.");
    }

    private static async Task TouchAsync(SqliteConnection connection, SqliteTransaction transaction, CrmWorkPackageId id, DateTimeOffset now, CancellationToken cancellationToken)
    {
        await using var touch = connection.CreateCommand();
        touch.Transaction = transaction;
        touch.CommandText = "UPDATE crm_work_packages SET version = version + 1, updated_utc = $utc WHERE work_package_id = $id;";
        touch.Parameters.AddWithValue("$id", id.Value);
        touch.Parameters.AddWithValue("$utc", now.ToString("O"));
        await touch.ExecuteNonQueryAsync(cancellationToken);
    }
}
