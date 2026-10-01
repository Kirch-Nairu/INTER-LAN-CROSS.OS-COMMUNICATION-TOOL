using InterLan.Domain.Crm;

namespace InterLan.Infrastructure.Crm;

public sealed partial class CrmAuthorityStore
{
    public async Task<IReadOnlyList<CrmCandidateEvidenceSnapshot>> ListCandidatesAsync(
        CrmWorkPackageId workPackageId,
        CancellationToken cancellationToken = default)
    {
        await using var connection = database.OpenConnection();
        await using var command = connection.CreateCommand();
        command.CommandText =
            """
            SELECT candidate_id, sequence, branch, source_sha, candidate_sha,
                   commit_count, submitted_by_user_id, submitted_utc
            FROM crm_candidate_submissions
            WHERE work_package_id = $id
            ORDER BY sequence;
            """;
        command.Parameters.AddWithValue("$id", workPackageId.Value);

        var results = new List<CrmCandidateEvidenceSnapshot>();
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            results.Add(new CrmCandidateEvidenceSnapshot(
                Guid.Parse(reader.GetString(0)),
                workPackageId,
                reader.GetInt32(1),
                reader.GetString(2),
                new CrmGitSha(reader.GetString(3)),
                new CrmGitSha(reader.GetString(4)),
                reader.GetInt32(5),
                Guid.Parse(reader.GetString(6)),
                DateTimeOffset.Parse(reader.GetString(7))));
        }
        return results;
    }

    public async Task<IReadOnlyList<CrmAuditSnapshot>> ListAuditAsync(
        CrmWorkPackageId workPackageId,
        CancellationToken cancellationToken = default)
    {
        await using var connection = database.OpenConnection();
        await using var command = connection.CreateCommand();
        command.CommandText =
            """
            SELECT audit_event_id, actor_user_id, candidate_id, event_type,
                   before_state, after_state, payload_json, created_utc
            FROM crm_audit_events
            WHERE work_package_id = $id
            ORDER BY created_utc, audit_event_id;
            """;
        command.Parameters.AddWithValue("$id", workPackageId.Value);

        var results = new List<CrmAuditSnapshot>();
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            results.Add(new CrmAuditSnapshot(
                Guid.Parse(reader.GetString(0)),
                Guid.Parse(reader.GetString(1)),
                reader.IsDBNull(2) ? null : Guid.Parse(reader.GetString(2)),
                reader.GetString(3),
                reader.IsDBNull(4) ? null : CrmWorkflowVocabulary.ParseState(reader.GetString(4)),
                reader.IsDBNull(5) ? null : CrmWorkflowVocabulary.ParseState(reader.GetString(5)),
                reader.GetString(6),
                DateTimeOffset.Parse(reader.GetString(7))));
        }
        return results;
    }

    public async Task<IReadOnlyList<CrmAssignmentSnapshot>> ListAssignmentsAsync(
        CrmWorkPackageId workPackageId,
        CancellationToken cancellationToken = default)
    {
        await using var connection = database.OpenConnection();
        await using var command = connection.CreateCommand();
        command.CommandText =
            """
            SELECT user_id, crm_role, assigned_by_user_id, assigned_utc
            FROM crm_assignments
            WHERE work_package_id = $id AND revoked_utc IS NULL
            ORDER BY assigned_utc, assignment_id;
            """;
        command.Parameters.AddWithValue("$id", workPackageId.Value);

        var results = new List<CrmAssignmentSnapshot>();
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            results.Add(new CrmAssignmentSnapshot(
                Guid.Parse(reader.GetString(0)),
                CrmCapabilityPolicy.ParseRole(reader.GetString(1)),
                Guid.Parse(reader.GetString(2)),
                DateTimeOffset.Parse(reader.GetString(3))));
        }
        return results;
    }
}

public sealed record CrmCandidateEvidenceSnapshot(
    Guid CandidateId,
    CrmWorkPackageId WorkPackageId,
    int Sequence,
    string Branch,
    CrmGitSha SourceSha,
    CrmGitSha CandidateSha,
    int CommitCount,
    Guid SubmittedByUserId,
    DateTimeOffset SubmittedUtc);

public sealed record CrmAuditSnapshot(
    Guid AuditEventId,
    Guid ActorUserId,
    Guid? CandidateId,
    string EventType,
    CrmWorkPackageState? BeforeState,
    CrmWorkPackageState? AfterState,
    string PayloadJson,
    DateTimeOffset CreatedUtc);

public sealed record CrmAssignmentSnapshot(
    Guid UserId,
    CrmStaffRole Role,
    Guid AssignedByUserId,
    DateTimeOffset AssignedUtc);
