namespace InterLan.Application.CrmHost;

public static class QuickTunnelUrlParser
{
    public static bool TryParse(string? line, out Uri? publicUri)
    {
        publicUri = null;
        if (string.IsNullOrWhiteSpace(line))
        {
            return false;
        }

        var searchStart = 0;
        while (searchStart < line.Length)
        {
            var start = line.IndexOf("https://", searchStart, StringComparison.OrdinalIgnoreCase);
            if (start < 0)
            {
                return false;
            }

            var end = start;
            while (end < line.Length &&
                   !char.IsWhiteSpace(line[end]) &&
                   line[end] is not ('"' or '\'' or '<' or '>'))
            {
                end++;
            }

            var candidateText = line[start..end].TrimEnd(',', ';', ')', ']', '}');
            if (Uri.TryCreate(candidateText, UriKind.Absolute, out var candidate) &&
                IsAllowedQuickTunnelUri(candidate))
            {
                publicUri = candidate;
                return true;
            }

            searchStart = start + "https://".Length;
        }

        return false;
    }

    private static bool IsAllowedQuickTunnelUri(Uri candidate) =>
        string.Equals(candidate.Scheme, Uri.UriSchemeHttps, StringComparison.OrdinalIgnoreCase) &&
        candidate.Host.EndsWith(".trycloudflare.com", StringComparison.OrdinalIgnoreCase) &&
        candidate.Host.Length > ".trycloudflare.com".Length &&
        !string.IsNullOrWhiteSpace(candidate.Host[..^".trycloudflare.com".Length]) &&
        candidate.Port == 443 &&
        string.IsNullOrEmpty(candidate.UserInfo) &&
        string.IsNullOrEmpty(candidate.Query) &&
        string.IsNullOrEmpty(candidate.Fragment) &&
        string.Equals(candidate.AbsolutePath, "/", StringComparison.Ordinal);
}
