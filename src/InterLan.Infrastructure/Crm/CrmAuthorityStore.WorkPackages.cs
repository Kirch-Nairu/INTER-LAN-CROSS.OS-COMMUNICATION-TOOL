using System.Text.Json;
using InterLan.Domain.Crm;

namespace InterLan.Infrastructure.Crm;

public sealed partial class CrmAuthorityStore
{
    public async Task CreateDraftWorkPackageAsync(
        CrmWorkPackageId workPackageId,
        Guid projectId,
        Guid featureId,
        string phase,
        CrmLayer layer,
        Guid technicalLeadUserId,
        CrmSourceIdentity source,
        string problem,
        string solution,
        CrmValidationPolicy validationPolicy,
        Guid actorUserId,
        DateTimeOffset now,
        CancellationToken cancellationToken = default)
    {
        if (projectId == Guid.Empty || featureId == Guid.Empty ||
            technicalLeadUserId == Guid.Empty || actorUserId == Guid.Empty)
        {
            throw new ArgumentException("CRM work-package identity fields cannot be empty.");
        }

        var normalizedSource = source.Normalize();
        var normalizedProblem = CrmRequirement.NormalizeText(problem, nameof(problem));
        var normalizedSolution = CrmRequirement.NormalizeText(solution, nameof(solution));
        ArgumentException.ThrowIfNullOrWhiteSpace(phase);

        await using var connection = database.OpenConnection();
        using var transaction = connection.BeginTransaction();

        await using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText =
            """
            INSERT INTO crm_work_packages (
                work_package_id, project_id, feature_id, phase, layer,
                technical_lead_user_id, state, source_repository, source_branch,
                source_sha, source_verified, problem, solution, ci_required,
                qa_required, screenshots_required, self_checks_json, version,
                created_by_user_id, created_utc, updated_utc)
            VALUES (
                $id, $project, $feature, $phase, $layer,
                $lead, 'DRAFT', $repository, $branch,
                $sourceSha, $verified, $problem, $solution, $ciRequired,
                $qaRequired, $screenshotsRequired, $selfChecks, 1,
                $actor, $createdUtc, $updatedUtc);
            """;
        command.Parameters.AddWithValue("$id", workPackageId.Value);
        command.Parameters.AddWithValue("$project", projectId.ToString("D"));
        command.Parameters.AddWithValue("$feature", featureId.ToString("D"));
        command.Parameters.AddWithValue("$phase", phase.Trim().ToUpperInvariant());
        command.Parameters.AddWithValue("$layer", CrmWorkflowVocabulary.Serialize(layer));
        command.Parameters.AddWithValue("$lead", technicalLeadUserId.ToString("D"));
        command.Parameters.AddWithValue("$repository", normalizedSource.Repository.Value);
        command.Parameters.AddWithValue("$branch", normalizedSource.Branch);
        command.Parameters.AddWithValue("$sourceSha", normalizedSource.Sha.Value);
        command.Parameters.AddWithValue("$verified", normalizedSource.Verified ? 1 : 0);
        command.Parameters.AddWithValue("$problem", normalizedProblem);
        command.Parameters.AddWithValue("$solution", normalizedSolution);
        command.Parameters.AddWithValue("$ciRequired", validationPolicy.CiRequired ? 1 : 0);
        command.Parameters.AddWithValue("$qaRequired", validationPolicy.QaRequired ? 1 : 0);
        command.Parameters.AddWithValue("$screenshotsRequired", validationPolicy.ScreenshotsRequired ? 1 : 0);
        command.Parameters.AddWithValue("$selfChecks", JsonSerializer.Serialize(validationPolicy.SelfChecks));
        command.Parameters.AddWithValue("$actor", actorUserId.ToString("D"));
        command.Parameters.AddWithValue("$createdUtc", now.ToString("O"));
        command.Parameters.AddWithValue("$updatedUtc", now.ToString("O"));
        await command.ExecuteNonQueryAsync(cancellationToken);

        await AppendAuditAsync(
            connection,
            transaction,
            actorUserId,
            workPackageId,
            null,
            "CRM_WORK_PACKAGE_DRAFTED",
            null,
            CrmWorkPackageState.Draft,
            new
            {
                projectId,
                featureId,
                phase = phase.Trim().ToUpperInvariant(),
                layer = CrmWorkflowVocabulary.Serialize(layer),
                sourceRepository = normalizedSource.Repository.Value,
                sourceBranch = normalizedSource.Branch,
                sourceSha = normalizedSource.Sha.Value,
                sourceVerified = normalizedSource.Verified
            },
            now,
            cancellationToken);

        transaction.Commit();
    }

    public async Task<CrmWorkPackageSnapshot?> GetWorkPackageAsync(
        CrmWorkPackageId workPackageId,
        CancellationToken cancellationToken = default)
    {
        await using var connection = database.OpenConnection();
        await using var command = connection.CreateCommand();
        command.CommandText =
            """
            SELECT project_id, feature_id, phase, layer, technical_lead_user_id,
                   state, source_repository, source_branch, source_sha, source_verified,
                   problem, solution, ci_required, qa_required, screenshots_required,
                   self_checks_json, version, created_utc, updated_utc
            FROM crm_work_packages
            WHERE work_package_id = $id;
            """;
        command.Parameters.AddWithValue("$id", workPackageId.Value);

        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        if (!await reader.ReadAsync(cancellationToken))
            return null;

        var selfChecks = JsonSerializer.Deserialize<string[]>(reader.GetString(15)) ?? [];
        return new CrmWorkPackageSnapshot(
            workPackageId,
            Guid.Parse(reader.GetString(0)),
            Guid.Parse(reader.GetString(1)),
            reader.GetString(2),
            CrmWorkflowVocabulary.ParseLayer(reader.GetString(3)),
            Guid.Parse(reader.GetString(4)),
            CrmWorkflowVocabulary.ParseState(reader.GetString(5)),
            new CrmSourceIdentity(
                new CrmRepositoryIdentity(reader.GetString(6)),
                reader.GetString(7),
                new CrmGitSha(reader.GetString(8)),
                reader.GetInt32(9) != 0),
            reader.GetString(10),
            reader.GetString(11),
            CrmValidationPolicy.Create(
                reader.GetInt32(12) != 0,
                reader.GetInt32(13) != 0,
                reader.GetInt32(14) != 0,
                selfChecks),
            reader.GetInt64(16),
            DateTimeOffset.Parse(reader.GetString(17)),
            DateTimeOffset.Parse(reader.GetString(18)));
    }
}

public sealed record CrmWorkPackageSnapshot(
    CrmWorkPackageId Id,
    Guid ProjectId,
    Guid FeatureId,
    string Phase,
    CrmLayer Layer,
    Guid TechnicalLeadUserId,
    CrmWorkPackageState State,
    CrmSourceIdentity Source,
    string Problem,
    string Solution,
    CrmValidationPolicy ValidationPolicy,
    long Version,
    DateTimeOffset CreatedUtc,
    DateTimeOffset UpdatedUtc);
