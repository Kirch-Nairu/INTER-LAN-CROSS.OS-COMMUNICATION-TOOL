using InterLan.Contracts.Crm;
using InterLan.Domain.Crm;
using InterLan.Infrastructure;
using InterLan.Infrastructure.Crm;

namespace InterLan.Server.Crm;

public static partial class CrmEndpointMappings
{
    public static IEndpointRouteBuilder MapInterLanCrmEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/v1/crm");

        group.MapPost("/projects", async (HttpContext context, CrmProjectRequest request, EnrollmentStore enrollment, CrmAuthorityStore authority, CancellationToken ct) =>
        {
            try
            {
                var actor = await CrmAuthorization.RequireCapabilityAsync(context, enrollment, authority, CrmCapability.ManageCatalog, ct);
                var id = Guid.NewGuid();
                await authority.CreateProjectAsync(id, request.Key, request.Name, actor.Principal.UserId, DateTimeOffset.UtcNow, ct);
                return Results.Ok(new { projectId = id });
            }
            catch (UnauthorizedAccessException) { return Results.Forbid(); }
            catch (Exception ex) when (ex is ArgumentException or InvalidOperationException) { return Results.BadRequest(new { error = ex.Message }); }
        });

        group.MapPost("/features", async (HttpContext context, CrmFeatureRequest request, EnrollmentStore enrollment, CrmAuthorityStore authority, CancellationToken ct) =>
        {
            try
            {
                var actor = await CrmAuthorization.RequireCapabilityAsync(context, enrollment, authority, CrmCapability.ManageCatalog, ct);
                var id = Guid.NewGuid();
                await authority.CreateFeatureAsync(id, request.ProjectId, request.Key, request.Name, actor.Principal.UserId, DateTimeOffset.UtcNow, ct);
                return Results.Ok(new { featureId = id });
            }
            catch (UnauthorizedAccessException) { return Results.Forbid(); }
            catch (Exception ex) when (ex is ArgumentException or InvalidOperationException) { return Results.BadRequest(new { error = ex.Message }); }
        });

        group.MapPost("/roles", async (HttpContext context, CrmRoleGrantRequest request, EnrollmentStore enrollment, CrmAuthorityStore authority, CancellationToken ct) =>
        {
            try
            {
                var actor = await CrmAuthorization.RequireCapabilityAsync(context, enrollment, authority, CrmCapability.ManageStaffRoles, ct);
                var role = CrmCapabilityPolicy.ParseRole(request.Role);
                await authority.GrantRoleAsync(request.UserId, role, actor.Principal.UserId, DateTimeOffset.UtcNow, ct);
                return Results.NoContent();
            }
            catch (UnauthorizedAccessException) { return Results.Forbid(); }
            catch (Exception ex) when (ex is ArgumentException or InvalidOperationException) { return Results.BadRequest(new { error = ex.Message }); }
        });

        group.MapPost("/work-packages", async (HttpContext context, CrmDraftWorkPackageRequest request, EnrollmentStore enrollment, CrmAuthorityStore authority, CancellationToken ct) =>
        {
            try
            {
                var actor = await CrmAuthorization.RequireCapabilityAsync(context, enrollment, authority, CrmCapability.DraftWorkPackage, ct);
                var id = new CrmWorkPackageId(request.WorkPackageId);
                await authority.CreateDraftWorkPackageAsync(
                    id,
                    request.ProjectId,
                    request.FeatureId,
                    request.Phase,
                    CrmWorkflowVocabulary.ParseLayer(request.Layer),
                    request.TechnicalLeadUserId,
                    new CrmSourceIdentity(new CrmRepositoryIdentity(request.Source.Repository), request.Source.Branch, new CrmGitSha(request.Source.Sha), request.Source.Verified),
                    request.Problem,
                    request.Solution,
                    CrmValidationPolicy.Create(request.ValidationPolicy.CiRequired, request.ValidationPolicy.QaRequired, request.ValidationPolicy.ScreenshotsRequired, request.ValidationPolicy.SelfChecks),
                    actor.Principal.UserId,
                    DateTimeOffset.UtcNow,
                    ct);
                return Results.Ok(new { workPackageId = id.Value, state = "DRAFT" });
            }
            catch (UnauthorizedAccessException) { return Results.Forbid(); }
            catch (Exception ex) when (ex is ArgumentException or InvalidOperationException) { return Results.BadRequest(new { error = ex.Message }); }
        });

        group.MapGet("/work-packages/{workPackageId}", async (HttpContext context, string workPackageId, EnrollmentStore enrollment, CrmAuthorityStore authority, CancellationToken ct) =>
        {
            try
            {
                await CrmAuthorization.RequireCapabilityAsync(context, enrollment, authority, CrmCapability.ReadAuthority, ct);
                var id = new CrmWorkPackageId(workPackageId);
                var snapshot = await authority.GetWorkPackageAsync(id, ct);
                if (snapshot is null)
                    return Results.NotFound();
                var accepted = await authority.GetAcceptedShaAsync(id, ct);
                return Results.Ok(ToContract(snapshot, accepted));
            }
            catch (UnauthorizedAccessException) { return Results.Unauthorized(); }
            catch (ArgumentException ex) { return Results.BadRequest(new { error = ex.Message }); }
        });

        group.MapPost("/work-packages/{workPackageId}/requirements", async (HttpContext context, string workPackageId, CrmRequirementRequest request, EnrollmentStore enrollment, CrmAuthorityStore authority, CancellationToken ct) =>
        {
            try
            {
                var id = new CrmWorkPackageId(workPackageId);
                var actor = await CrmAuthorization.RequireWorkPackageLeadAsync(context, enrollment, authority, id, CrmCapability.DraftWorkPackage, ct);
                await authority.AddRequirementAsync(id, new CrmRequirement(request.Id, request.Text), actor.Principal.UserId, DateTimeOffset.UtcNow, ct);
                return Results.NoContent();
            }
            catch (UnauthorizedAccessException) { return Results.Forbid(); }
            catch (Exception ex) when (ex is ArgumentException or InvalidOperationException or KeyNotFoundException) { return Results.BadRequest(new { error = ex.Message }); }
        });

        group.MapPost("/work-packages/{workPackageId}/deliverables", async (HttpContext context, string workPackageId, CrmDeliverableRequest request, EnrollmentStore enrollment, CrmAuthorityStore authority, CancellationToken ct) =>
        {
            try
            {
                var id = new CrmWorkPackageId(workPackageId);
                var actor = await CrmAuthorization.RequireWorkPackageLeadAsync(context, enrollment, authority, id, CrmCapability.DraftWorkPackage, ct);
                await authority.AddDeliverableAsync(id, new CrmDeliverable(request.Id, request.Text), actor.Principal.UserId, DateTimeOffset.UtcNow, ct);
                return Results.NoContent();
            }
            catch (UnauthorizedAccessException) { return Results.Forbid(); }
            catch (Exception ex) when (ex is ArgumentException or InvalidOperationException or KeyNotFoundException) { return Results.BadRequest(new { error = ex.Message }); }
        });

        group.MapPost("/work-packages/{workPackageId}/criteria", async (HttpContext context, string workPackageId, CrmAcceptanceCriterionRequest request, EnrollmentStore enrollment, CrmAuthorityStore authority, CancellationToken ct) =>
        {
            try
            {
                var id = new CrmWorkPackageId(workPackageId);
                var actor = await CrmAuthorization.RequireWorkPackageLeadAsync(context, enrollment, authority, id, CrmCapability.DraftWorkPackage, ct);
                await authority.AddAcceptanceCriterionAsync(id, new CrmAcceptanceCriterion(request.Id, request.Text, request.EvidenceKinds), actor.Principal.UserId, DateTimeOffset.UtcNow, ct);
                return Results.NoContent();
            }
            catch (UnauthorizedAccessException) { return Results.Forbid(); }
            catch (Exception ex) when (ex is ArgumentException or InvalidOperationException or KeyNotFoundException) { return Results.BadRequest(new { error = ex.Message }); }
        });

        group.MapPost("/work-packages/{workPackageId}/scope", async (HttpContext context, string workPackageId, CrmScopeRuleRequest request, EnrollmentStore enrollment, CrmAuthorityStore authority, CancellationToken ct) =>
        {
            try
            {
                var id = new CrmWorkPackageId(workPackageId);
                var actor = await CrmAuthorization.RequireWorkPackageLeadAsync(context, enrollment, authority, id, CrmCapability.DraftWorkPackage, ct);
                var kind = request.Kind.Trim().ToUpperInvariant() switch { "OWNED" => CrmScopeKind.Owned, "PROHIBITED" => CrmScopeKind.Prohibited, _ => throw new ArgumentException("Scope kind must be OWNED or PROHIBITED.") };
                await authority.AddScopeRuleAsync(id, new CrmScopeRule(kind, request.Pattern), actor.Principal.UserId, DateTimeOffset.UtcNow, ct);
                return Results.NoContent();
            }
            catch (UnauthorizedAccessException) { return Results.Forbid(); }
            catch (Exception ex) when (ex is ArgumentException or InvalidOperationException or KeyNotFoundException) { return Results.BadRequest(new { error = ex.Message }); }
        });

        group.MapPost("/work-packages/{workPackageId}/assignments", async (HttpContext context, string workPackageId, CrmAssignmentRequest request, EnrollmentStore enrollment, CrmAuthorityStore authority, CancellationToken ct) =>
        {
            try
            {
                var id = new CrmWorkPackageId(workPackageId);
                var actor = await CrmAuthorization.RequireWorkPackageLeadAsync(context, enrollment, authority, id, CrmCapability.AssignWork, ct);
                await authority.AssignAsync(id, request.UserId, CrmCapabilityPolicy.ParseRole(request.Role), actor.Principal.UserId, DateTimeOffset.UtcNow, ct);
                return Results.NoContent();
            }
            catch (UnauthorizedAccessException) { return Results.Forbid(); }
            catch (Exception ex) when (ex is ArgumentException or InvalidOperationException or KeyNotFoundException) { return Results.BadRequest(new { error = ex.Message }); }
        });

        MapWorkflowEndpoints(group);
        MapImportEndpoints(group);
        return app;
    }

    private static CrmWorkPackageContract ToContract(CrmWorkPackageSnapshot snapshot, CrmGitSha? acceptedSha) =>
        new(
            snapshot.Id.Value,
            snapshot.ProjectId,
            snapshot.FeatureId,
            snapshot.Phase,
            CrmWorkflowVocabulary.Serialize(snapshot.Layer),
            snapshot.TechnicalLeadUserId,
            CrmWorkflowVocabulary.Serialize(snapshot.State),
            new CrmSourceContract(snapshot.Source.Repository.Value, snapshot.Source.Branch, snapshot.Source.Sha.Value, snapshot.Source.Verified),
            snapshot.Problem,
            snapshot.Solution,
            new CrmValidationPolicyContract(snapshot.ValidationPolicy.CiRequired, snapshot.ValidationPolicy.QaRequired, snapshot.ValidationPolicy.ScreenshotsRequired, snapshot.ValidationPolicy.SelfChecks),
            snapshot.Version,
            snapshot.CreatedUtc,
            snapshot.UpdatedUtc,
            acceptedSha?.Value);
}
