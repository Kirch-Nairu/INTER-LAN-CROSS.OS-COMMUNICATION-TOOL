namespace InterLan.Domain.Crm;

public sealed record CrmAssignment(
    Guid UserId,
    CrmStaffRole Role,
    DateTimeOffset AssignedUtc,
    Guid AssignedByUserId);

public static class CrmAssignmentPolicy
{
    public static bool IsCompatible(CrmStaffRole role, CrmLayer layer) =>
        role switch
        {
            CrmStaffRole.TechnicalLead => true,
            CrmStaffRole.BackendDeveloper => layer is CrmLayer.Backend or CrmLayer.Infrastructure or CrmLayer.CrossLayer,
            CrmStaffRole.FrontendDeveloper => layer is CrmLayer.Frontend or CrmLayer.CrossLayer,
            CrmStaffRole.CiCdValidator => layer is CrmLayer.Validation or CrmLayer.CrossLayer,
            CrmStaffRole.QualityAssurance => layer is CrmLayer.Validation or CrmLayer.CrossLayer,
            CrmStaffRole.Observer => false,
            CrmStaffRole.System => false,
            _ => false
        };

    public static void RequireCompatible(CrmStaffRole role, CrmLayer layer)
    {
        if (!IsCompatible(role, layer))
            throw new InvalidOperationException($"CRM role {role} is not compatible with work-package layer {layer}.");
    }

    public static void RequireAssignedImplementationActor(
        Guid actorUserId,
        CrmStaffRole actorRole,
        CrmLayer layer,
        IEnumerable<CrmAssignment> assignments)
    {
        CrmCapabilityPolicy.Require(actorRole, CrmCapability.ImplementAssignedWork);
        RequireCompatible(actorRole, layer);

        if (!assignments.Any(assignment =>
                assignment.UserId == actorUserId &&
                assignment.Role == actorRole))
        {
            throw new UnauthorizedAccessException("Implementation authority requires an explicit compatible work-package assignment.");
        }
    }
}
