namespace InterLan.Domain.Crm;

public readonly record struct CrmWorkPackageId
{
    public CrmWorkPackageId(string value)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(value);
        var normalized = value.Trim().ToUpperInvariant();

        if (normalized.Length > 64 ||
            normalized.Any(character =>
                !(character is >= 'A' and <= 'Z' or >= '0' and <= '9' or '-' or '_')))
        {
            throw new ArgumentException(
                "Work-package IDs must be 1-64 characters using A-Z, 0-9, '-' or '_'.",
                nameof(value));
        }

        Value = normalized;
    }

    public string Value { get; }
    public override string ToString() => Value;
}

public readonly record struct CrmGitSha
{
    public CrmGitSha(string value)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(value);
        var normalized = value.Trim().ToLowerInvariant();

        if ((normalized.Length != 40 && normalized.Length != 64) ||
            normalized.Any(character => !char.IsAsciiHexDigit(character)))
        {
            throw new ArgumentException(
                "Git SHA evidence must be an exact 40- or 64-character hexadecimal object ID.",
                nameof(value));
        }

        Value = normalized;
    }

    public string Value { get; }
    public override string ToString() => Value;
}

public sealed record CrmRepositoryIdentity
{
    public CrmRepositoryIdentity(string value)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(value);
        var normalized = value.Trim();
        var slash = normalized.IndexOf('/');

        if (slash <= 0 ||
            slash == normalized.Length - 1 ||
            normalized.IndexOf('/', slash + 1) >= 0 ||
            normalized.Any(char.IsWhiteSpace))
        {
            throw new ArgumentException(
                "Repository identity must use the 'owner/name' form without whitespace.",
                nameof(value));
        }

        Value = normalized;
    }

    public string Value { get; }
    public override string ToString() => Value;
}

public sealed record CrmSourceIdentity(
    CrmRepositoryIdentity Repository,
    string Branch,
    CrmGitSha Sha,
    bool Verified)
{
    public CrmSourceIdentity Normalize()
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(Branch);
        return this with { Branch = Branch.Trim() };
    }
}
