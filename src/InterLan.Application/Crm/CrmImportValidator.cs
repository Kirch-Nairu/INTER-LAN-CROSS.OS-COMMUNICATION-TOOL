using InterLan.Contracts.Crm;
using InterLan.Domain.Crm;

namespace InterLan.Application.Crm;

public interface ICrmImportAuthorityLookup
{
    Task<bool> ProjectExistsAsync(Guid projectId, CancellationToken cancellationToken = default);
    Task<bool> FeatureExistsAsync(Guid projectId, Guid featureId, CancellationToken cancellationToken = default);
    Task<CrmImportResolvedStaff?> ResolveStaffAsync(string identity, CancellationToken cancellationToken = default);
}

public sealed record CrmImportResolvedStaff(Guid UserId, IReadOnlyList<CrmStaffRole> Roles);
public sealed record CrmResolvedAssignment(Guid UserId, CrmStaffRole Role, string SourceIdentity);

public sealed record CrmValidatedImport(
    CrmWorkPackageId WorkPackageId,
    Guid ProjectId,
    Guid FeatureId,
    string Phase,
    CrmLayer Layer,
    Guid TechnicalLeadUserId,
    CrmSourceIdentity Source,
    string Problem,
    string Solution,
    IReadOnlyList<CrmRequirement> Requirements,
    IReadOnlyList<CrmDeliverable> Deliverables,
    IReadOnlyList<CrmAcceptanceCriterion> AcceptanceCriteria,
    IReadOnlyList<CrmScopeRule> Scope,
    IReadOnlyList<CrmResolvedAssignment> Assignments,
    CrmValidationPolicy ValidationPolicy,
    IReadOnlyList<string> Warnings);

public sealed class CrmImportValidator
{
    private readonly ICrmImportAuthorityLookup lookup;

    public CrmImportValidator(ICrmImportAuthorityLookup lookup)
    {
        this.lookup = lookup ?? throw new ArgumentNullException(nameof(lookup));
    }

    public async Task<CrmValidatedImport> ValidateAsync(
        CrmWorkPackageImportV1 payload,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(payload);

        var id = new CrmWorkPackageId(payload.WorkPackageId);
        if (!await lookup.ProjectExistsAsync(payload.ProjectId, cancellationToken))
            throw new InvalidDataException($"CRM import project '{payload.ProjectId}' does not exist.");
        if (!await lookup.FeatureExistsAsync(payload.ProjectId, payload.FeatureId, cancellationToken))
            throw new InvalidDataException($"CRM import feature '{payload.FeatureId}' does not belong to project '{payload.ProjectId}'.");

        var layer = CrmWorkflowVocabulary.ParseLayer(payload.Layer);
        var source = new CrmSourceIdentity(
            new CrmRepositoryIdentity(payload.Source.Repository),
            CrmRequirement.NormalizeText(payload.Source.Branch, nameof(payload.Source.Branch)),
            new CrmGitSha(payload.Source.Sha),
            payload.Source.Verified).Normalize();

        var lead = await lookup.ResolveStaffAsync(payload.TechnicalLead, cancellationToken)
            ?? throw new InvalidDataException($"Technical Lead identity '{payload.TechnicalLead}' does not resolve to active staff.");
        if (!lead.Roles.Contains(CrmStaffRole.TechnicalLead))
            throw new InvalidDataException($"Identity '{payload.TechnicalLead}' does not hold Technical Lead authority.");

        var requirements = RequireItems(payload.Requirements, "requirements")
            .Select(item => new CrmRequirement(item.Id, item.Text))
            .ToArray();
        var deliverables = RequireItems(payload.Deliverables, "deliverables")
            .Select(item => new CrmDeliverable(item.Id, item.Text))
            .ToArray();
        if (payload.AcceptanceCriteria is null || payload.AcceptanceCriteria.Count == 0)
            throw new InvalidDataException("CRM import requires at least one acceptance criterion.");
        var criteria = payload.AcceptanceCriteria
            .Select(item => new CrmAcceptanceCriterion(item.Id, item.Text, item.EvidenceKinds))
            .ToArray();

        var scope = (payload.Scope ?? Array.Empty<CrmImportScopeRule>())
            .Select(rule => new CrmScopeRule(ParseScopeKind(rule.Kind), rule.Pattern))
            .ToArray();
        var collisions = scope
            .GroupBy(rule => rule.Pattern, StringComparer.Ordinal)
            .Where(group => group.Select(rule => rule.Kind).Distinct().Count() > 1)
            .Select(group => group.Key)
            .ToArray();
        if (collisions.Length > 0)
            throw new InvalidDataException($"CRM import scope patterns cannot be both owned and prohibited: {string.Join(", ", collisions)}.");

        if (payload.Assignments is null || payload.Assignments.Count == 0)
            throw new InvalidDataException("CRM import requires at least one assignment.");

        var assignments = new List<CrmResolvedAssignment>();
        foreach (var assignment in payload.Assignments)
        {
            var role = CrmCapabilityPolicy.ParseRole(assignment.Role);
            CrmAssignmentPolicy.RequireCompatible(role, layer);
            var staff = await lookup.ResolveStaffAsync(assignment.Staff, cancellationToken)
                ?? throw new InvalidDataException($"Assigned staff identity '{assignment.Staff}' does not resolve.");
            if (!staff.Roles.Contains(role))
                throw new InvalidDataException($"Assigned staff '{assignment.Staff}' does not hold declared role '{assignment.Role}'.");
            assignments.Add(new CrmResolvedAssignment(staff.UserId, role, assignment.Staff));
        }

        var validation = CrmValidationPolicy.Create(
            payload.Validation.CiRequired,
            payload.Validation.QaRequired,
            payload.Validation.ScreenshotsRequired,
            payload.Validation.SelfChecks);

        var warnings = new List<string>();
        if (!source.Verified)
            warnings.Add("Source identity is explicitly unverified; authorization will remain blocked until source verification is recorded.");
        if (scope.Length == 0)
            warnings.Add("No explicit owned/prohibited scope rules were supplied.");

        return new CrmValidatedImport(
            id,
            payload.ProjectId,
            payload.FeatureId,
            CrmRequirement.NormalizeKey(payload.Phase, nameof(payload.Phase)),
            layer,
            lead.UserId,
            source,
            CrmRequirement.NormalizeText(payload.Problem, nameof(payload.Problem)),
            CrmRequirement.NormalizeText(payload.Solution, nameof(payload.Solution)),
            requirements,
            deliverables,
            criteria,
            scope,
            assignments,
            validation,
            warnings);
    }

    private static IReadOnlyList<CrmImportTextItem> RequireItems(IReadOnlyList<CrmImportTextItem>? items, string label)
    {
        if (items is null || items.Count == 0)
            throw new InvalidDataException($"CRM import requires at least one {label} item.");
        return items;
    }

    private static CrmScopeKind ParseScopeKind(string value) =>
        value.Trim().ToUpperInvariant() switch
        {
            "OWNED" => CrmScopeKind.Owned,
            "PROHIBITED" => CrmScopeKind.Prohibited,
            _ => throw new InvalidDataException($"Unsupported CRM scope kind '{value}'.")
        };
}
