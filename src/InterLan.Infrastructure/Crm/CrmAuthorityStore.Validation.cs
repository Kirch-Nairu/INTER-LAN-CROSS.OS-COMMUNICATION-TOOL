using InterLan.Domain.Crm;

namespace InterLan.Infrastructure.Crm;

public sealed partial class CrmAuthorityStore
{
    public async Task<long> RecordValidationAsync(
        CrmWorkPackageId workPackageId,
        long expectedVersion,
        CrmCandidateValidation validation,
        CrmStaffRole actorRole,
        DateTimeOffset now,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(validation);
        if (validation.Result == CrmValidationResult.Running)
            throw new InvalidOperationException("Persisted validation results must be terminal PASS or FAIL.");

        var capability = validation.Gate switch
        {
            CrmValidationGate.Ci => CrmCapability.RecordCiValidation,
            CrmValidationGate.Qa => CrmCapability.RecordQaValidation,
            _ => throw new InvalidOperationException("Lead disposition is not a CI/QA validation record.")
        };
        CrmCapabilityPolicy.Require(actorRole, capability);

        await using var connection = database.OpenConnection();
        using var transaction = connection.BeginTransaction();
        var snapshot = await ReadSnapshotAsync(connection, transaction, workPackageId, cancellationToken);
        if (snapshot.Version != expectedVersion)
            throw new InvalidOperationException("Stale CRM work-package version during validation recording.");

        var requiredState = validation.Gate == CrmValidationGate.Ci
            ? CrmWorkPackageState.CiRunning
            : CrmWorkPackageState.QaRunning;
        if (snapshot.State != requiredState)
            throw new InvalidOperationException($"{validation.Gate} result requires state {requiredState}.");

        var currentCandidate = await GetCurrentCandidateIdentityAsync(connection, transaction, workPackageId, cancellationToken);
        if (currentCandidate.CandidateId != validation.CandidateId || currentCandidate.CandidateSha != validation.CandidateSha)
            throw new InvalidOperationException("Validation must reference the current exact candidate ID and SHA.");

        if (validation.Gate == CrmValidationGate.Qa && snapshot.ValidationPolicy.CiRequired)
            await RequireCurrentCandidatePassAsync(connection, transaction, workPackageId, "CI", cancellationToken);

        await using (var insert = connection.CreateCommand())
        {
            insert.Transaction = transaction;
            insert.CommandText =
                """
                INSERT INTO crm_candidate_validations (
                    validation_id, candidate_id, candidate_sha, gate, result,
                    actor_user_id, summary, recorded_utc)
                VALUES ($id, $candidate, $sha, $gate, $result, $actor, $summary, $utc);
                """;
            insert.Parameters.AddWithValue("$id", validation.ValidationId.ToString("D"));
            insert.Parameters.AddWithValue("$candidate", validation.CandidateId.ToString("D"));
            insert.Parameters.AddWithValue("$sha", validation.CandidateSha.Value);
            insert.Parameters.AddWithValue("$gate", validation.Gate == CrmValidationGate.Ci ? "CI" : "QA");
            insert.Parameters.AddWithValue("$result", validation.Result == CrmValidationResult.Passed ? "PASSED" : "FAILED");
            insert.Parameters.AddWithValue("$actor", validation.ActorUserId.ToString("D"));
            insert.Parameters.AddWithValue("$summary", (object?)validation.Summary ?? DBNull.Value);
            insert.Parameters.AddWithValue("$utc", validation.RecordedUtc.ToString("O"));
            await insert.ExecuteNonQueryAsync(cancellationToken);
        }

        var nextState = (validation.Gate, validation.Result) switch
        {
            (CrmValidationGate.Ci, CrmValidationResult.Passed) => CrmWorkPackageState.CiPassed,
            (CrmValidationGate.Ci, CrmValidationResult.Failed) => CrmWorkPackageState.CiFailed,
            (CrmValidationGate.Qa, CrmValidationResult.Passed) => CrmWorkPackageState.QaPassed,
            (CrmValidationGate.Qa, CrmValidationResult.Failed) => CrmWorkPackageState.QaFailed,
            _ => throw new InvalidOperationException("Unsupported validation state.")
        };

        await using (var update = connection.CreateCommand())
        {
            update.Transaction = transaction;
            update.CommandText =
                """
                UPDATE crm_work_packages
                SET state = $state, version = version + 1, updated_utc = $utc
                WHERE work_package_id = $id AND version = $expectedVersion;
                """;
            update.Parameters.AddWithValue("$state", CrmWorkflowVocabulary.Serialize(nextState));
            update.Parameters.AddWithValue("$utc", now.ToString("O"));
            update.Parameters.AddWithValue("$id", workPackageId.Value);
            update.Parameters.AddWithValue("$expectedVersion", expectedVersion);
            if (await update.ExecuteNonQueryAsync(cancellationToken) != 1)
                throw new InvalidOperationException("Validation recording lost an optimistic concurrency race.");
        }

        await AppendAuditAsync(connection, transaction, validation.ActorUserId, workPackageId, validation.CandidateId, $"CRM_{validation.Gate.ToString().ToUpperInvariant()}_{validation.Result.ToString().ToUpperInvariant()}", snapshot.State, nextState, new { candidateSha = validation.CandidateSha.Value, validation.Summary }, now, cancellationToken);
        transaction.Commit();
        return expectedVersion + 1;
    }

    private static async Task<(Guid CandidateId, CrmGitSha CandidateSha)> GetCurrentCandidateIdentityAsync(
        Microsoft.Data.Sqlite.SqliteConnection connection,
        Microsoft.Data.Sqlite.SqliteTransaction transaction,
        CrmWorkPackageId id,
        CancellationToken cancellationToken)
    {
        await using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText =
            """
            SELECT candidate_id, candidate_sha
            FROM crm_candidate_submissions
            WHERE work_package_id = $id
            ORDER BY sequence DESC
            LIMIT 1;
            """;
        command.Parameters.AddWithValue("$id", id.Value);
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        if (!await reader.ReadAsync(cancellationToken))
            throw new InvalidOperationException("The work package has no submitted candidate.");
        return (Guid.Parse(reader.GetString(0)), new CrmGitSha(reader.GetString(1)));
    }
}
