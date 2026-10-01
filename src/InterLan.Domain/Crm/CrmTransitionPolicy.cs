namespace InterLan.Domain.Crm;

public static class CrmTransitionPolicy
{
    private static readonly IReadOnlyDictionary<CrmWorkPackageState, CrmWorkPackageState[]> Allowed =
        new Dictionary<CrmWorkPackageState, CrmWorkPackageState[]>
        {
            [CrmWorkPackageState.Draft] = [CrmWorkPackageState.Authorized],
            [CrmWorkPackageState.Authorized] = [CrmWorkPackageState.Implementing],
            [CrmWorkPackageState.Implementing] = [CrmWorkPackageState.CandidateSubmitted],
            [CrmWorkPackageState.CandidateSubmitted] = [CrmWorkPackageState.CiPending],
            [CrmWorkPackageState.CiPending] = [CrmWorkPackageState.CiRunning],
            [CrmWorkPackageState.CiRunning] = [CrmWorkPackageState.CiFailed, CrmWorkPackageState.CiPassed],
            [CrmWorkPackageState.CiFailed] = [CrmWorkPackageState.Implementing],
            [CrmWorkPackageState.CiPassed] = [CrmWorkPackageState.QaPending],
            [CrmWorkPackageState.QaPending] = [CrmWorkPackageState.QaRunning],
            [CrmWorkPackageState.QaRunning] = [CrmWorkPackageState.QaFailed, CrmWorkPackageState.QaPassed],
            [CrmWorkPackageState.QaFailed] = [CrmWorkPackageState.Implementing],
            [CrmWorkPackageState.QaPassed] = [CrmWorkPackageState.LeadReview],
            [CrmWorkPackageState.LeadReview] = [CrmWorkPackageState.LeadRework, CrmWorkPackageState.LeadAccepted],
            [CrmWorkPackageState.LeadRework] = [CrmWorkPackageState.Implementing],
            [CrmWorkPackageState.LeadAccepted] = [CrmWorkPackageState.IntegrationAuthorized],
            [CrmWorkPackageState.IntegrationAuthorized] = [CrmWorkPackageState.Integrated],
            [CrmWorkPackageState.Integrated] = [CrmWorkPackageState.PhaseFrozen],
            [CrmWorkPackageState.PhaseFrozen] = [CrmWorkPackageState.Closed],
            [CrmWorkPackageState.Closed] = []
        };

    public static IReadOnlyList<CrmWorkPackageState> NextStates(CrmWorkPackageState current) =>
        Allowed.TryGetValue(current, out var next) ? next : Array.Empty<CrmWorkPackageState>();

    public static bool CanTransition(CrmWorkPackageState current, CrmWorkPackageState next) =>
        NextStates(current).Contains(next);

    public static void RequireAllowed(CrmWorkPackageState current, CrmWorkPackageState next)
    {
        if (!CanTransition(current, next))
            throw new InvalidOperationException($"Illegal CRM work-package transition: {current} -> {next}.");
    }

    public static CrmCapability RequiredCapability(
        CrmWorkPackageState current,
        CrmWorkPackageState next)
    {
        RequireAllowed(current, next);

        return (current, next) switch
        {
            (CrmWorkPackageState.Draft, CrmWorkPackageState.Authorized) => CrmCapability.AuthorizeWorkPackage,
            (CrmWorkPackageState.Authorized, CrmWorkPackageState.Implementing) => CrmCapability.ImplementAssignedWork,
            (CrmWorkPackageState.Implementing, CrmWorkPackageState.CandidateSubmitted) => CrmCapability.SubmitCandidate,
            (CrmWorkPackageState.CandidateSubmitted, CrmWorkPackageState.CiPending) => CrmCapability.RecordCiValidation,
            (CrmWorkPackageState.CiPending, CrmWorkPackageState.CiRunning) => CrmCapability.RecordCiValidation,
            (CrmWorkPackageState.CiRunning, CrmWorkPackageState.CiFailed or CrmWorkPackageState.CiPassed) => CrmCapability.RecordCiValidation,
            (CrmWorkPackageState.CiFailed, CrmWorkPackageState.Implementing) => CrmCapability.ImplementAssignedWork,
            (CrmWorkPackageState.CiPassed, CrmWorkPackageState.QaPending) => CrmCapability.RecordQaValidation,
            (CrmWorkPackageState.QaPending, CrmWorkPackageState.QaRunning) => CrmCapability.RecordQaValidation,
            (CrmWorkPackageState.QaRunning, CrmWorkPackageState.QaFailed or CrmWorkPackageState.QaPassed) => CrmCapability.RecordQaValidation,
            (CrmWorkPackageState.QaFailed, CrmWorkPackageState.Implementing) => CrmCapability.ImplementAssignedWork,
            (CrmWorkPackageState.QaPassed, CrmWorkPackageState.LeadReview) => CrmCapability.LeadDisposition,
            (CrmWorkPackageState.LeadReview, CrmWorkPackageState.LeadRework or CrmWorkPackageState.LeadAccepted) => CrmCapability.LeadDisposition,
            (CrmWorkPackageState.LeadRework, CrmWorkPackageState.Implementing) => CrmCapability.ImplementAssignedWork,
            (CrmWorkPackageState.LeadAccepted, CrmWorkPackageState.IntegrationAuthorized) => CrmCapability.AuthorizeIntegration,
            (CrmWorkPackageState.IntegrationAuthorized, CrmWorkPackageState.Integrated) => CrmCapability.AuthorizeIntegration,
            (CrmWorkPackageState.Integrated, CrmWorkPackageState.PhaseFrozen) => CrmCapability.FreezePhase,
            (CrmWorkPackageState.PhaseFrozen, CrmWorkPackageState.Closed) => CrmCapability.ClosePhase,
            _ => throw new InvalidOperationException($"No capability mapping exists for {current} -> {next}.")
        };
    }
}
