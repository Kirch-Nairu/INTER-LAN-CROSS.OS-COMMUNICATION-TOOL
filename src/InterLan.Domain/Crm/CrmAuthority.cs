namespace InterLan.Domain.Crm;

public enum CrmStaffRole
{
    TechnicalLead,
    BackendDeveloper,
    FrontendDeveloper,
    CiCdValidator,
    QualityAssurance,
    Observer,
    System
}

public enum CrmCapability
{
    ReadAuthority,
    ManageCatalog,
    ManageStaffRoles,
    DraftWorkPackage,
    AuthorizeWorkPackage,
    AssignWork,
    ImplementAssignedWork,
    SubmitCandidate,
    RecordCiValidation,
    RecordQaValidation,
    LeadDisposition,
    AuthorizeIntegration,
    FreezePhase,
    ClosePhase,
    ImportDraft
}

public static class CrmCapabilityPolicy
{
    public static bool Allows(CrmStaffRole role, CrmCapability capability) =>
        role switch
        {
            CrmStaffRole.TechnicalLead => true,
            CrmStaffRole.BackendDeveloper => capability is
                CrmCapability.ReadAuthority or
                CrmCapability.ImplementAssignedWork or
                CrmCapability.SubmitCandidate,
            CrmStaffRole.FrontendDeveloper => capability is
                CrmCapability.ReadAuthority or
                CrmCapability.ImplementAssignedWork or
                CrmCapability.SubmitCandidate,
            CrmStaffRole.CiCdValidator => capability is
                CrmCapability.ReadAuthority or
                CrmCapability.RecordCiValidation,
            CrmStaffRole.QualityAssurance => capability is
                CrmCapability.ReadAuthority or
                CrmCapability.RecordQaValidation,
            CrmStaffRole.Observer => capability == CrmCapability.ReadAuthority,
            CrmStaffRole.System => capability is
                CrmCapability.ReadAuthority or
                CrmCapability.DraftWorkPackage,
            _ => false
        };

    public static void Require(CrmStaffRole role, CrmCapability capability)
    {
        if (!Allows(role, capability))
            throw new UnauthorizedAccessException($"CRM role {role} lacks capability {capability}.");
    }

    public static string Serialize(CrmStaffRole role) =>
        role switch
        {
            CrmStaffRole.TechnicalLead => "TECHNICAL_LEAD",
            CrmStaffRole.BackendDeveloper => "BACKEND_DEVELOPER",
            CrmStaffRole.FrontendDeveloper => "FRONTEND_DEVELOPER",
            CrmStaffRole.CiCdValidator => "CI_CD_VALIDATOR",
            CrmStaffRole.QualityAssurance => "QUALITY_ASSURANCE",
            _ => role.ToString().ToUpperInvariant()
        };

    public static CrmStaffRole ParseRole(string value)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(value);
        var normalized = value.Trim().Replace("_", string.Empty, StringComparison.Ordinal);

        foreach (var role in Enum.GetValues<CrmStaffRole>())
        {
            var candidate = Serialize(role).Replace("_", string.Empty, StringComparison.Ordinal);
            if (string.Equals(candidate, normalized, StringComparison.OrdinalIgnoreCase))
                return role;
        }

        throw new ArgumentOutOfRangeException(nameof(value), value, "Unsupported CRM staff role.");
    }
}
