namespace InterLan.Domain.Crm;

public sealed record CrmRequirement
{
    public CrmRequirement(string id, string text)
    {
        Id = NormalizeKey(id, nameof(id));
        Text = NormalizeText(text, nameof(text));
    }

    public string Id { get; }
    public string Text { get; }

    internal static string NormalizeKey(string value, string parameterName)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(value, parameterName);
        var normalized = value.Trim().ToUpperInvariant();
        if (normalized.Length > 64 || normalized.Any(char.IsWhiteSpace))
            throw new ArgumentException("Structured CRM keys must be non-empty, whitespace-free and at most 64 characters.", parameterName);
        return normalized;
    }

    internal static string NormalizeText(string value, string parameterName)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(value, parameterName);
        var normalized = value.Trim();
        if (normalized.Length > 8000)
            throw new ArgumentException("CRM text fields may not exceed 8000 characters.", parameterName);
        return normalized;
    }
}

public sealed record CrmAcceptanceCriterion
{
    public CrmAcceptanceCriterion(
        string id,
        string text,
        IEnumerable<string>? evidenceKinds = null)
    {
        Id = CrmRequirement.NormalizeKey(id, nameof(id));
        Text = CrmRequirement.NormalizeText(text, nameof(text));
        EvidenceKinds = (evidenceKinds ?? Array.Empty<string>())
            .Select(kind => CrmRequirement.NormalizeKey(kind, nameof(evidenceKinds)))
            .Distinct(StringComparer.Ordinal)
            .ToArray();
    }

    public string Id { get; }
    public string Text { get; }
    public IReadOnlyList<string> EvidenceKinds { get; }
}

public enum CrmScopeKind
{
    Owned,
    Prohibited
}

public sealed record CrmScopeRule
{
    public CrmScopeRule(CrmScopeKind kind, string pattern)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(pattern);
        var normalized = pattern.Trim().Replace('\\', '/');
        if (normalized.StartsWith('/', StringComparison.Ordinal) ||
            normalized.Contains("../", StringComparison.Ordinal) ||
            normalized == "..")
        {
            throw new ArgumentException("Scope patterns must be repository-relative and cannot traverse upward.", nameof(pattern));
        }

        Kind = kind;
        Pattern = normalized;
    }

    public CrmScopeKind Kind { get; }
    public string Pattern { get; }
}

public sealed record CrmValidationPolicy(
    bool CiRequired,
    bool QaRequired,
    bool ScreenshotsRequired,
    IReadOnlyList<string> SelfChecks)
{
    public static CrmValidationPolicy Create(
        bool ciRequired,
        bool qaRequired,
        bool screenshotsRequired,
        IEnumerable<string>? selfChecks = null) =>
        new(
            ciRequired,
            qaRequired,
            screenshotsRequired,
            (selfChecks ?? Array.Empty<string>())
                .Select(check => CrmRequirement.NormalizeText(check, nameof(selfChecks)))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToArray());
}
