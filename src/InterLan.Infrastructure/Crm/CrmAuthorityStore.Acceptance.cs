using InterLan.Domain.Crm;

namespace InterLan.Infrastructure.Crm;

public sealed partial class CrmAuthorityStore
{
    public async Task<long> RecordAcceptedShaAsync(
        CrmWorkPackageId workPackageId,
        long expectedVersion,
        Guid candidateId,
        CrmGitSha candidateSha,
        Guid actorUserId,
        CrmStaffRole actorRole,
        DateTimeOffset now,
        CancellationToken cancellationToken = default)
    {
        CrmCapabilityPolicy.Require(actorRole, CrmCapability.LeadDisposition);

        await using var connection = database.OpenConnection();
        using var transaction = connection.BeginTransaction();
        var snapshot = await ReadSnapshotAsync(connection, transaction, workPackageId, cancellationToken);
        if (snapshot.Version != expectedVersion)
            throw new InvalidOperationException("Stale CRM work-package version during acceptance recording.");
        if (snapshot.State != CrmWorkPackageState.LeadAccepted)
            throw new InvalidOperationException("Accepted SHA may only be recorded after LEAD_ACCEPTED.");
        if (snapshot.TechnicalLeadUserId != actorUserId)
            throw new UnauthorizedAccessException("Only the work package Technical Lead may record accepted SHA evidence.");

        var currentCandidate = await GetCurrentCandidateIdentityAsync(connection, transaction, workPackageId, cancellationToken);
        if (currentCandidate.CandidateId != candidateId || currentCandidate.CandidateSha != candidateSha)
            throw new InvalidOperationException("Accepted SHA must identify the current exact candidate.");

        if (snapshot.ValidationPolicy.CiRequired)
            await RequireCurrentCandidatePassAsync(connection, transaction, workPackageId, "CI", cancellationToken);
        if (snapshot.ValidationPolicy.QaRequired)
            await RequireCurrentCandidatePassAsync(connection, transaction, workPackageId, "QA", cancellationToken);

        await using (var insert = connection.CreateCommand())
        {
            insert.Transaction = transaction;
            insert.CommandText =
                """
                INSERT INTO crm_work_package_acceptance (
                    work_package_id, candidate_id, accepted_sha, accepted_by_user_id, accepted_utc)
                VALUES ($workPackage, $candidate, $sha, $actor, $utc);
                """;
            insert.Parameters.AddWithValue("$workPackage", workPackageId.Value);
            insert.Parameters.AddWithValue("$candidate", candidateId.ToString("D"));
            insert.Parameters.AddWithValue("$sha", candidateSha.Value);
            insert.Parameters.AddWithValue("$actor", actorUserId.ToString("D"));
            insert.Parameters.AddWithValue("$utc", now.ToString("O"));
            await insert.ExecuteNonQueryAsync(cancellationToken);
        }

        await using (var update = connection.CreateCommand())
        {
            update.Transaction = transaction;
            update.CommandText =
                """
                UPDATE crm_work_packages
                SET version = version + 1, updated_utc = $utc
                WHERE work_package_id = $id AND version = $expectedVersion;
                """;
            update.Parameters.AddWithValue("$utc", now.ToString("O"));
            update.Parameters.AddWithValue("$id", workPackageId.Value);
            update.Parameters.AddWithValue("$expectedVersion", expectedVersion);
            if (await update.ExecuteNonQueryAsync(cancellationToken) != 1)
                throw new InvalidOperationException("Acceptance recording lost an optimistic concurrency race.");
        }

        await AppendAuditAsync(connection, transaction, actorUserId, workPackageId, candidateId, "CRM_CANDIDATE_ACCEPTED", snapshot.State, snapshot.State, new { acceptedSha = candidateSha.Value }, now, cancellationToken);
        transaction.Commit();
        return expectedVersion + 1;
    }

    public async Task<CrmGitSha?> GetAcceptedShaAsync(
        CrmWorkPackageId workPackageId,
        CancellationToken cancellationToken = default)
    {
        await using var connection = database.OpenConnection();
        await using var command = connection.CreateCommand();
        command.CommandText = "SELECT accepted_sha FROM crm_work_package_acceptance WHERE work_package_id = $id;";
        command.Parameters.AddWithValue("$id", workPackageId.Value);
        var result = await command.ExecuteScalarAsync(cancellationToken);
        return result is null || result is DBNull ? null : new CrmGitSha(Convert.ToString(result)!);
    }
}
