using InterLan.Contracts.Crm;
using InterLan.Domain.Crm;
using InterLan.Infrastructure;
using InterLan.Infrastructure.Crm;

namespace InterLan.Server.Crm;

public static partial class CrmEndpointMappings
{
    private static void MapWorkflowEndpoints(RouteGroupBuilder group)
    {
        group.MapPost("/work-packages/{workPackageId}/transition", async (HttpContext context, string workPackageId, CrmTransitionRequest request, EnrollmentStore enrollment, CrmAuthorityStore authority, CancellationToken ct) =>
        {
            try
            {
                var id = new CrmWorkPackageId(workPackageId);
                var snapshot = await authority.GetWorkPackageAsync(id, ct) ?? throw new KeyNotFoundException($"CRM work package {id} was not found.");
                var next = CrmWorkflowVocabulary.ParseState(request.NextState);
                var capability = CrmTransitionPolicy.RequiredCapability(snapshot.State, next);
                var leadOnly = capability is CrmCapability.AuthorizeWorkPackage or CrmCapability.LeadDisposition or CrmCapability.AuthorizeIntegration or CrmCapability.FreezePhase or CrmCapability.ClosePhase;
                var actor = leadOnly
                    ? await CrmAuthorization.RequireWorkPackageLeadAsync(context, enrollment, authority, id, capability, ct)
                    : await CrmAuthorization.RequireCapabilityAsync(context, enrollment, authority, capability, ct);
                var version = await authority.TransitionAsync(id, request.ExpectedVersion, next, actor.Principal.UserId, actor.Role, DateTimeOffset.UtcNow, ct);
                return Results.Ok(new { state = CrmWorkflowVocabulary.Serialize(next), version });
            }
            catch (UnauthorizedAccessException) { return Results.Forbid(); }
            catch (Exception ex) when (ex is ArgumentException or InvalidOperationException or KeyNotFoundException) { return Results.BadRequest(new { error = ex.Message }); }
        });

        group.MapPost("/work-packages/{workPackageId}/candidates", async (HttpContext context, string workPackageId, CrmCandidateSubmissionRequest request, EnrollmentStore enrollment, CrmAuthorityStore authority, CancellationToken ct) =>
        {
            try
            {
                var id = new CrmWorkPackageId(workPackageId);
                var actor = await CrmAuthorization.RequireCapabilityAsync(context, enrollment, authority, CrmCapability.SubmitCandidate, ct);
                var candidate = new CrmCandidateSubmission(
                    request.CandidateId,
                    id,
                    request.Sequence,
                    request.Branch,
                    new CrmGitSha(request.SourceSha),
                    new CrmGitSha(request.CandidateSha),
                    request.CommitCount,
                    request.CommitRange,
                    request.ChangedFiles.Select(file => new CrmChangedFileEvidence(file.Path, file.ChangeType, file.AddedLines, file.DeletedLines)).ToArray(),
                    request.SelfValidation,
                    actor.Principal.UserId,
                    DateTimeOffset.UtcNow,
                    request.KnownLimitations,
                    request.NotesToValidator);
                var version = await authority.SubmitCandidateAsync(candidate, request.ExpectedVersion, actor.Principal.UserId, actor.Role, DateTimeOffset.UtcNow, ct);
                return Results.Ok(new { candidateId = candidate.CandidateId, candidateSha = candidate.CandidateSha.Value, version });
            }
            catch (UnauthorizedAccessException) { return Results.Forbid(); }
            catch (Exception ex) when (ex is ArgumentException or InvalidOperationException) { return Results.BadRequest(new { error = ex.Message }); }
        });

        group.MapPost("/work-packages/{workPackageId}/ci-result", async (HttpContext context, string workPackageId, CrmValidationResultRequest request, EnrollmentStore enrollment, CrmAuthorityStore authority, CancellationToken ct) =>
            await RecordValidationEndpoint(context, workPackageId, request, CrmValidationGate.Ci, enrollment, authority, ct));

        group.MapPost("/work-packages/{workPackageId}/qa-result", async (HttpContext context, string workPackageId, CrmValidationResultRequest request, EnrollmentStore enrollment, CrmAuthorityStore authority, CancellationToken ct) =>
            await RecordValidationEndpoint(context, workPackageId, request, CrmValidationGate.Qa, enrollment, authority, ct));

        group.MapPost("/work-packages/{workPackageId}/accepted-sha", async (HttpContext context, string workPackageId, CrmAcceptanceRequest request, EnrollmentStore enrollment, CrmAuthorityStore authority, CancellationToken ct) =>
        {
            try
            {
                var id = new CrmWorkPackageId(workPackageId);
                var actor = await CrmAuthorization.RequireWorkPackageLeadAsync(context, enrollment, authority, id, CrmCapability.LeadDisposition, ct);
                var version = await authority.RecordAcceptedShaAsync(id, request.ExpectedVersion, request.CandidateId, new CrmGitSha(request.CandidateSha), actor.Principal.UserId, actor.Role, DateTimeOffset.UtcNow, ct);
                return Results.Ok(new { acceptedSha = request.CandidateSha.ToLowerInvariant(), version });
            }
            catch (UnauthorizedAccessException) { return Results.Forbid(); }
            catch (Exception ex) when (ex is ArgumentException or InvalidOperationException or KeyNotFoundException) { return Results.BadRequest(new { error = ex.Message }); }
        });

        group.MapGet("/work-packages/{workPackageId}/candidates", async (HttpContext context, string workPackageId, EnrollmentStore enrollment, CrmAuthorityStore authority, CancellationToken ct) =>
        {
            try
            {
                await CrmAuthorization.RequireCapabilityAsync(context, enrollment, authority, CrmCapability.ReadAuthority, ct);
                var candidates = await authority.ListCandidatesAsync(new CrmWorkPackageId(workPackageId), ct);
                return Results.Ok(candidates.Select(candidate => new CrmCandidateContract(candidate.CandidateId, candidate.WorkPackageId.Value, candidate.Sequence, candidate.Branch, candidate.SourceSha.Value, candidate.CandidateSha.Value, candidate.CommitCount, candidate.SubmittedByUserId, candidate.SubmittedUtc)));
            }
            catch (UnauthorizedAccessException) { return Results.Unauthorized(); }
            catch (ArgumentException ex) { return Results.BadRequest(new { error = ex.Message }); }
        });

        group.MapGet("/work-packages/{workPackageId}/audit", async (HttpContext context, string workPackageId, EnrollmentStore enrollment, CrmAuthorityStore authority, CancellationToken ct) =>
        {
            try
            {
                await CrmAuthorization.RequireCapabilityAsync(context, enrollment, authority, CrmCapability.ReadAuthority, ct);
                var audit = await authority.ListAuditAsync(new CrmWorkPackageId(workPackageId), ct);
                return Results.Ok(audit.Select(item => new CrmAuditEventContract(item.AuditEventId, item.ActorUserId, item.CandidateId, item.EventType, item.BeforeState is { } before ? CrmWorkflowVocabulary.Serialize(before) : null, item.AfterState is { } after ? CrmWorkflowVocabulary.Serialize(after) : null, item.PayloadJson, item.CreatedUtc)));
            }
            catch (UnauthorizedAccessException) { return Results.Unauthorized(); }
            catch (ArgumentException ex) { return Results.BadRequest(new { error = ex.Message }); }
        });
    }

    private static async Task<IResult> RecordValidationEndpoint(
        HttpContext context,
        string workPackageId,
        CrmValidationResultRequest request,
        CrmValidationGate gate,
        EnrollmentStore enrollment,
        CrmAuthorityStore authority,
        CancellationToken ct)
    {
        try
        {
            var capability = gate == CrmValidationGate.Ci ? CrmCapability.RecordCiValidation : CrmCapability.RecordQaValidation;
            var actor = await CrmAuthorization.RequireCapabilityAsync(context, enrollment, authority, capability, ct);
            var result = request.Result.Trim().ToUpperInvariant() switch
            {
                "PASSED" or "PASS" => CrmValidationResult.Passed,
                "FAILED" or "FAIL" => CrmValidationResult.Failed,
                _ => throw new ArgumentException("Validation result must be PASSED or FAILED.")
            };
            var validation = new CrmCandidateValidation(request.ValidationId, request.CandidateId, new CrmGitSha(request.CandidateSha), gate, result, actor.Principal.UserId, DateTimeOffset.UtcNow, request.Summary);
            var version = await authority.RecordValidationAsync(new CrmWorkPackageId(workPackageId), request.ExpectedVersion, validation, actor.Role, DateTimeOffset.UtcNow, ct);
            return Results.Ok(new { gate = gate.ToString().ToUpperInvariant(), result = result.ToString().ToUpperInvariant(), version });
        }
        catch (UnauthorizedAccessException) { return Results.Forbid(); }
        catch (Exception ex) when (ex is ArgumentException or InvalidOperationException) { return Results.BadRequest(new { error = ex.Message }); }
    }
}
