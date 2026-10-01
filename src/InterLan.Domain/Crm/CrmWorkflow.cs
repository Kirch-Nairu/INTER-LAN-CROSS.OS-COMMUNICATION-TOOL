namespace InterLan.Domain.Crm;

public enum CrmWorkPackageState
{
    Draft,
    Authorized,
    Implementing,
    CandidateSubmitted,
    CiPending,
    CiRunning,
    CiFailed,
    CiPassed,
    QaPending,
    QaRunning,
    QaFailed,
    QaPassed,
    LeadReview,
    LeadRework,
    LeadAccepted,
    IntegrationAuthorized,
    Integrated,
    PhaseFrozen,
    Closed
}

public enum CrmLayer
{
    Backend,
    Frontend,
    Infrastructure,
    Validation,
    CrossLayer
}

public static class CrmWorkflowVocabulary
{
    public static string Serialize(CrmWorkPackageState state) =>
        state switch
        {
            CrmWorkPackageState.CiPending => "CI_PENDING",
            CrmWorkPackageState.CiRunning => "CI_RUNNING",
            CrmWorkPackageState.CiFailed => "CI_FAILED",
            CrmWorkPackageState.CiPassed => "CI_PASSED",
            CrmWorkPackageState.QaPending => "QA_PENDING",
            CrmWorkPackageState.QaRunning => "QA_RUNNING",
            CrmWorkPackageState.QaFailed => "QA_FAILED",
            CrmWorkPackageState.QaPassed => "QA_PASSED",
            CrmWorkPackageState.LeadReview => "LEAD_REVIEW",
            CrmWorkPackageState.LeadRework => "LEAD_REWORK",
            CrmWorkPackageState.LeadAccepted => "LEAD_ACCEPTED",
            CrmWorkPackageState.IntegrationAuthorized => "INTEGRATION_AUTHORIZED",
            CrmWorkPackageState.PhaseFrozen => "PHASE_FROZEN",
            CrmWorkPackageState.CandidateSubmitted => "CANDIDATE_SUBMITTED",
            _ => state.ToString().ToUpperInvariant()
        };

    public static CrmWorkPackageState ParseState(string value)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(value);
        var normalized = value.Trim().Replace("_", string.Empty, StringComparison.Ordinal);

        foreach (var state in Enum.GetValues<CrmWorkPackageState>())
        {
            var candidate = state.ToString().Replace("_", string.Empty, StringComparison.Ordinal);
            if (string.Equals(candidate, normalized, StringComparison.OrdinalIgnoreCase))
                return state;
        }

        throw new ArgumentOutOfRangeException(nameof(value), value, "Unsupported CRM work-package state.");
    }

    public static string Serialize(CrmLayer layer) =>
        layer switch
        {
            CrmLayer.CrossLayer => "CROSS_LAYER",
            _ => layer.ToString().ToUpperInvariant()
        };

    public static CrmLayer ParseLayer(string value)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(value);
        var normalized = value.Trim().Replace("_", string.Empty, StringComparison.Ordinal);

        foreach (var layer in Enum.GetValues<CrmLayer>())
        {
            var candidate = layer.ToString().Replace("_", string.Empty, StringComparison.Ordinal);
            if (string.Equals(candidate, normalized, StringComparison.OrdinalIgnoreCase))
                return layer;
        }

        throw new ArgumentOutOfRangeException(nameof(value), value, "Unsupported CRM layer.");
    }
}
