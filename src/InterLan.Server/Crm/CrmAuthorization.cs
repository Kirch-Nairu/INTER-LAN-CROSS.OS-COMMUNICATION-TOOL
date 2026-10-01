using InterLan.Application;
using InterLan.Domain.Crm;
using InterLan.Infrastructure;
using InterLan.Infrastructure.Crm;

namespace InterLan.Server.Crm;

public sealed record CrmAuthorityActor(SessionPrincipal Principal, CrmStaffRole Role);

public static class CrmAuthorization
{
    public static async Task<CrmAuthorityActor> RequireCapabilityAsync(
        HttpContext context,
        EnrollmentStore enrollment,
        CrmAuthorityStore authority,
        CrmCapability capability,
        CancellationToken cancellationToken)
    {
        var principal = await AuthorizationHelpers.RequireAuthenticatedAsync(context, enrollment, cancellationToken);
        if (string.Equals(principal.Role, "OWNER", StringComparison.Ordinal))
        {
            CrmCapabilityPolicy.Require(CrmStaffRole.TechnicalLead, capability);
            return new CrmAuthorityActor(principal, CrmStaffRole.TechnicalLead);
        }

        var roles = await authority.GetActiveRolesAsync(principal.UserId, cancellationToken);
        var role = roles.FirstOrDefault(candidate => CrmCapabilityPolicy.Allows(candidate, capability));
        if (!roles.Contains(role) || !CrmCapabilityPolicy.Allows(role, capability))
            throw new UnauthorizedAccessException($"CRM capability '{capability}' is required.");

        return new CrmAuthorityActor(principal, role);
    }

    public static async Task<CrmAuthorityActor> RequireWorkPackageLeadAsync(
        HttpContext context,
        EnrollmentStore enrollment,
        CrmAuthorityStore authority,
        CrmWorkPackageId workPackageId,
        CrmCapability capability,
        CancellationToken cancellationToken)
    {
        var actor = await RequireCapabilityAsync(context, enrollment, authority, capability, cancellationToken);
        if (actor.Role != CrmStaffRole.TechnicalLead)
            throw new UnauthorizedAccessException("Technical Lead CRM authority required.");

        var workPackage = await authority.GetWorkPackageAsync(workPackageId, cancellationToken)
            ?? throw new KeyNotFoundException($"CRM work package {workPackageId} was not found.");
        if (workPackage.TechnicalLeadUserId != actor.Principal.UserId)
            throw new UnauthorizedAccessException("Only the designated Technical Lead may perform this work-package action.");

        return actor;
    }
}
