using System.Text.RegularExpressions;

namespace InterLan.Application.CrmHost;

public static partial class QuickTunnelUrlParser
{
    [GeneratedRegex(@"https://[a-z0-9](?:[a-z0-9-]{0,62}[a-z0-9])?\.trycloudflare\.com", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex CandidateRegex();

    public static bool TryParse(string? line, out Uri? publicUri)
    {
        publicUri = null;
        if (string.IsNullOrWhiteSpace(line))
        {
            return false;
        }

        var match = CandidateRegex().Match(line);
        if (!match.Success || !Uri.TryCreate(match.Value, UriKind.Absolute, out var candidate))
        {
            return false;
        }

        if (!string.Equals(candidate.Scheme, Uri.UriSchemeHttps, StringComparison.OrdinalIgnoreCase) ||
            !candidate.Host.EndsWith(".trycloudflare.com", StringComparison.OrdinalIgnoreCase) ||
            candidate.Host.Length <= ".trycloudflare.com".Length ||
            !string.IsNullOrEmpty(candidate.UserInfo) ||
            !string.IsNullOrEmpty(candidate.Query) ||
            !string.IsNullOrEmpty(candidate.Fragment) ||
            !string.Equals(candidate.AbsolutePath, "/", StringComparison.Ordinal))
        {
            return false;
        }

        publicUri = candidate;
        return true;
    }
}
