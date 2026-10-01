using System.Text.Json;
using InterLan.Domain.Crm;

namespace InterLan.Infrastructure.Crm;

public sealed partial class CrmAuthorityStore
{
    public async Task<long> SubmitCandidateAsync(
        CrmCandidateSubmission candidate,
        long expectedVersion,
        Guid actorUserId,
        CrmStaffRole actorRole,
        DateTimeOffset now,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(candidate);
        CrmCapabilityPolicy.Require(actorRole, CrmCapability.SubmitCandidate);

        await using var connection = database.OpenConnection();
        using var transaction = connection.BeginTransaction();
        var snapshot = await ReadSnapshotAsync(connection, transaction, candidate.WorkPackageId, cancellationToken);
        if (snapshot.State != CrmWorkPackageState.Implementing)
            throw new InvalidOperationException("Candidates may only be submitted while IMPLEMENTING.");
        if (snapshot.Version != expectedVersion)
            throw new InvalidOperationException("Stale CRM work-package version during candidate submission.");
        if (candidate.SourceSha != snapshot.Source.Sha)
            throw new InvalidOperationException("Candidate source SHA does not match authorized source SHA.");

        CrmAssignmentPolicy.RequireCompatible(actorRole, snapshot.Layer);
        await using (var assignment = connection.CreateCommand())
        {
            assignment.Transaction = transaction;
            assignment.CommandText =
                """
                SELECT COUNT(1) FROM crm_assignments
                WHERE work_package_id = $id AND user_id = $user
                  AND crm_role = $role AND revoked_utc IS NULL;
                """;
            assignment.Parameters.AddWithValue("$id", candidate.WorkPackageId.Value);
            assignment.Parameters.AddWithValue("$user", actorUserId.ToString("D"));
            assignment.Parameters.AddWithValue("$role", CrmCapabilityPolicy.Serialize(actorRole));
            if (Convert.ToInt64(await assignment.ExecuteScalarAsync(cancellationToken)) == 0)
                throw new UnauthorizedAccessException("Candidate submission requires an active compatible assignment.");
        }

        await using (var sequence = connection.CreateCommand())
        {
            sequence.Transaction = transaction;
            sequence.CommandText = "SELECT COALESCE(MAX(sequence), 0) + 1 FROM crm_candidate_submissions WHERE work_package_id = $id;";
            sequence.Parameters.AddWithValue("$id", candidate.WorkPackageId.Value);
            var required = Convert.ToInt32(await sequence.ExecuteScalarAsync(cancellationToken));
            if (candidate.Sequence != required)
                throw new InvalidOperationException($"Candidate sequence must be append-only. Required sequence: {required}.");
        }

        await using (var insert = connection.CreateCommand())
        {
            insert.Transaction = transaction;
            insert.CommandText =
                """
                INSERT INTO crm_candidate_submissions (
                    candidate_id, work_package_id, sequence, branch, source_sha, candidate_sha,
                    commit_count, commit_range, self_validation_json, known_limitations,
                    notes_to_validator, submitted_by_user_id, submitted_utc)
                VALUES ($candidate, $workPackage, $sequence, $branch, $sourceSha, $candidateSha,
                    $commitCount, $commitRange, $selfValidation, $limitations,
                    $notes, $actor, $utc);
                """;
            insert.Parameters.AddWithValue("$candidate", candidate.CandidateId.ToString("D"));
            insert.Parameters.AddWithValue("$workPackage", candidate.WorkPackageId.Value);
            insert.Parameters.AddWithValue("$sequence", candidate.Sequence);
            insert.Parameters.AddWithValue("$branch", candidate.Branch);
            insert.Parameters.AddWithValue("$sourceSha", candidate.SourceSha.Value);
            insert.Parameters.AddWithValue("$candidateSha", candidate.CandidateSha.Value);
            insert.Parameters.AddWithValue("$commitCount", candidate.CommitCount);
            insert.Parameters.AddWithValue("$commitRange", (object?)candidate.CommitRange ?? DBNull.Value);
            insert.Parameters.AddWithValue("$selfValidation", JsonSerializer.Serialize(candidate.SelfValidation));
            insert.Parameters.AddWithValue("$limitations", (object?)candidate.KnownLimitations ?? DBNull.Value);
            insert.Parameters.AddWithValue("$notes", (object?)candidate.NotesToValidator ?? DBNull.Value);
            insert.Parameters.AddWithValue("$actor", actorUserId.ToString("D"));
            insert.Parameters.AddWithValue("$utc", candidate.SubmittedUtc.ToString("O"));
            await insert.ExecuteNonQueryAsync(cancellationToken);
        }

        foreach (var file in candidate.ChangedFiles)
        {
            await using var changedFile = connection.CreateCommand();
            changedFile.Transaction = transaction;
            changedFile.CommandText =
                """
                INSERT INTO crm_candidate_files (
                    candidate_file_id, candidate_id, path, change_type, added_lines, deleted_lines)
                VALUES ($id, $candidate, $path, $changeType, $added, $deleted);
                """;
            changedFile.Parameters.AddWithValue("$id", Guid.NewGuid().ToString("D"));
            changedFile.Parameters.AddWithValue("$candidate", candidate.CandidateId.ToString("D"));
            changedFile.Parameters.AddWithValue("$path", file.Path);
            changedFile.Parameters.AddWithValue("$changeType", file.ChangeType);
            changedFile.Parameters.AddWithValue("$added", file.AddedLines);
            changedFile.Parameters.AddWithValue("$deleted", file.DeletedLines);
            await changedFile.ExecuteNonQueryAsync(cancellationToken);
        }

        await using (var update = connection.CreateCommand())
        {
            update.Transaction = transaction;
            update.CommandText =
                """
                UPDATE crm_work_packages
                SET state = 'CANDIDATE_SUBMITTED', version = version + 1, updated_utc = $utc
                WHERE work_package_id = $id AND version = $expectedVersion;
                """;
            update.Parameters.AddWithValue("$utc", now.ToString("O"));
            update.Parameters.AddWithValue("$id", candidate.WorkPackageId.Value);
            update.Parameters.AddWithValue("$expectedVersion", expectedVersion);
            if (await update.ExecuteNonQueryAsync(cancellationToken) != 1)
                throw new InvalidOperationException("Candidate submission lost an optimistic concurrency race.");
        }

        await AppendAuditAsync(connection, transaction, actorUserId, candidate.WorkPackageId, candidate.CandidateId, "CRM_CANDIDATE_SUBMITTED", snapshot.State, CrmWorkPackageState.CandidateSubmitted, new { candidate.Sequence, candidateSha = candidate.CandidateSha.Value, candidate.SourceSha, candidate.CommitCount }, now, cancellationToken);
        transaction.Commit();
        return expectedVersion + 1;
    }
}
