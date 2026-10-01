using InterLan.Domain.Crm;
using Microsoft.Data.Sqlite;

namespace InterLan.Infrastructure.Crm;

public sealed partial class CrmAuthorityStore
{
    public async Task<long> TransitionAsync(
        CrmWorkPackageId id,
        long expectedVersion,
        CrmWorkPackageState next,
        Guid actorUserId,
        CrmStaffRole actorRole,
        DateTimeOffset now,
        CancellationToken cancellationToken = default)
    {
        await using var connection = database.OpenConnection();
        using var transaction = connection.BeginTransaction();
        var current = await ReadSnapshotAsync(connection, transaction, id, cancellationToken);
        if (current.Version != expectedVersion)
            throw new InvalidOperationException($"Stale CRM work-package version. Expected {expectedVersion}, current {current.Version}.");

        var capability = CrmTransitionPolicy.RequiredCapability(current.State, next);
        CrmCapabilityPolicy.Require(actorRole, capability);
        await RequireTransitionPrerequisitesAsync(connection, transaction, current, next, actorUserId, actorRole, cancellationToken);

        await using var update = connection.CreateCommand();
        update.Transaction = transaction;
        update.CommandText =
            """
            UPDATE crm_work_packages
            SET state = $next, version = version + 1, updated_utc = $utc
            WHERE work_package_id = $id AND version = $expectedVersion;
            """;
        update.Parameters.AddWithValue("$next", CrmWorkflowVocabulary.Serialize(next));
        update.Parameters.AddWithValue("$utc", now.ToString("O"));
        update.Parameters.AddWithValue("$id", id.Value);
        update.Parameters.AddWithValue("$expectedVersion", expectedVersion);
        if (await update.ExecuteNonQueryAsync(cancellationToken) != 1)
            throw new InvalidOperationException("CRM work-package transition lost an optimistic concurrency race.");

        await AppendAuditAsync(connection, transaction, actorUserId, id, null, "CRM_STATE_TRANSITIONED", current.State, next, new { expectedVersion, nextVersion = expectedVersion + 1 }, now, cancellationToken);
        transaction.Commit();
        return expectedVersion + 1;
    }

    private static async Task RequireTransitionPrerequisitesAsync(
        SqliteConnection connection,
        SqliteTransaction transaction,
        CrmWorkPackageSnapshot current,
        CrmWorkPackageState next,
        Guid actorUserId,
        CrmStaffRole actorRole,
        CancellationToken cancellationToken)
    {
        if (next == CrmWorkPackageState.Authorized)
        {
            await RequireCountAsync(connection, transaction, "crm_requirements", current.Id, "requirement", cancellationToken);
            await RequireCountAsync(connection, transaction, "crm_deliverables", current.Id, "deliverable", cancellationToken);
            await RequireCountAsync(connection, transaction, "crm_acceptance_criteria", current.Id, "acceptance criterion", cancellationToken);
            await RequireCountAsync(connection, transaction, "crm_assignments", current.Id, "assignment", cancellationToken, "AND revoked_utc IS NULL");
        }

        if (next == CrmWorkPackageState.Implementing)
        {
            CrmAssignmentPolicy.RequireCompatible(actorRole, current.Layer);
            await using var assignment = connection.CreateCommand();
            assignment.Transaction = transaction;
            assignment.CommandText =
                """
                SELECT COUNT(1)
                FROM crm_assignments
                WHERE work_package_id = $id AND user_id = $user
                  AND crm_role = $role AND revoked_utc IS NULL;
                """;
            assignment.Parameters.AddWithValue("$id", current.Id.Value);
            assignment.Parameters.AddWithValue("$user", actorUserId.ToString("D"));
            assignment.Parameters.AddWithValue("$role", CrmCapabilityPolicy.Serialize(actorRole));
            if (Convert.ToInt64(await assignment.ExecuteScalarAsync(cancellationToken)) == 0)
                throw new UnauthorizedAccessException("Implementation authority requires an explicit active assignment.");
        }

        if (next == CrmWorkPackageState.QaPending && current.ValidationPolicy.CiRequired)
            await RequireCurrentCandidatePassAsync(connection, transaction, current.Id, "CI", cancellationToken);

        if (next is CrmWorkPackageState.LeadReview or CrmWorkPackageState.LeadAccepted)
        {
            if (current.ValidationPolicy.CiRequired)
                await RequireCurrentCandidatePassAsync(connection, transaction, current.Id, "CI", cancellationToken);
            if (current.ValidationPolicy.QaRequired)
                await RequireCurrentCandidatePassAsync(connection, transaction, current.Id, "QA", cancellationToken);
        }

        if (next == CrmWorkPackageState.IntegrationAuthorized)
        {
            await using var accepted = connection.CreateCommand();
            accepted.Transaction = transaction;
            accepted.CommandText = "SELECT COUNT(1) FROM crm_work_package_acceptance WHERE work_package_id = $id;";
            accepted.Parameters.AddWithValue("$id", current.Id.Value);
            if (Convert.ToInt64(await accepted.ExecuteScalarAsync(cancellationToken)) == 0)
                throw new InvalidOperationException("Integration cannot be authorized before an explicit accepted SHA is persisted.");
        }
    }

    private static async Task RequireCountAsync(
        SqliteConnection connection,
        SqliteTransaction transaction,
        string table,
        CrmWorkPackageId id,
        string label,
        CancellationToken cancellationToken,
        string extraPredicate = "")
    {
        await using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = $"SELECT COUNT(1) FROM {table} WHERE work_package_id = $id {extraPredicate};";
        command.Parameters.AddWithValue("$id", id.Value);
        if (Convert.ToInt64(await command.ExecuteScalarAsync(cancellationToken)) == 0)
            throw new InvalidOperationException($"CRM work package cannot advance without at least one {label}.");
    }

    private static async Task RequireCurrentCandidatePassAsync(
        SqliteConnection connection,
        SqliteTransaction transaction,
        CrmWorkPackageId id,
        string gate,
        CancellationToken cancellationToken)
    {
        await using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText =
            """
            SELECT COUNT(1)
            FROM crm_candidate_validations v
            JOIN crm_candidate_submissions c ON c.candidate_id = v.candidate_id
            WHERE c.work_package_id = $id
              AND c.sequence = (SELECT MAX(sequence) FROM crm_candidate_submissions WHERE work_package_id = $id)
              AND v.candidate_sha = c.candidate_sha
              AND v.gate = $gate
              AND v.result = 'PASSED';
            """;
        command.Parameters.AddWithValue("$id", id.Value);
        command.Parameters.AddWithValue("$gate", gate);
        if (Convert.ToInt64(await command.ExecuteScalarAsync(cancellationToken)) == 0)
            throw new InvalidOperationException($"{gate} PASS is required for the current exact candidate.");
    }

    private static async Task<CrmWorkPackageSnapshot> ReadSnapshotAsync(
        SqliteConnection connection,
        SqliteTransaction transaction,
        CrmWorkPackageId id,
        CancellationToken cancellationToken)
    {
        await using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText =
            """
            SELECT project_id, feature_id, phase, layer, technical_lead_user_id,
                   state, source_repository, source_branch, source_sha, source_verified,
                   problem, solution, ci_required, qa_required, screenshots_required,
                   self_checks_json, version, created_utc, updated_utc
            FROM crm_work_packages WHERE work_package_id = $id;
            """;
        command.Parameters.AddWithValue("$id", id.Value);
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        if (!await reader.ReadAsync(cancellationToken))
            throw new KeyNotFoundException($"CRM work package {id} was not found.");

        var selfChecks = System.Text.Json.JsonSerializer.Deserialize<string[]>(reader.GetString(15)) ?? [];
        return new CrmWorkPackageSnapshot(
            id,
            Guid.Parse(reader.GetString(0)),
            Guid.Parse(reader.GetString(1)),
            reader.GetString(2),
            CrmWorkflowVocabulary.ParseLayer(reader.GetString(3)),
            Guid.Parse(reader.GetString(4)),
            CrmWorkflowVocabulary.ParseState(reader.GetString(5)),
            new CrmSourceIdentity(new CrmRepositoryIdentity(reader.GetString(6)), reader.GetString(7), new CrmGitSha(reader.GetString(8)), reader.GetInt32(9) != 0),
            reader.GetString(10),
            reader.GetString(11),
            CrmValidationPolicy.Create(reader.GetInt32(12) != 0, reader.GetInt32(13) != 0, reader.GetInt32(14) != 0, selfChecks),
            reader.GetInt64(16),
            DateTimeOffset.Parse(reader.GetString(17)),
            DateTimeOffset.Parse(reader.GetString(18)));
    }
}
