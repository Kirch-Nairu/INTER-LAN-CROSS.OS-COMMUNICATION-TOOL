namespace InterLan.Application.CrmHost;

public enum HostHealthSeverity
{
    Info = 0,
    Warning = 1,
    Error = 2
}

public sealed record HostHealthFinding(
    string Code,
    HostHealthSeverity Severity,
    string Message);

public sealed record HostStartupHealth(IReadOnlyList<HostHealthFinding> Findings)
{
    public bool CanStartLocalHost => Findings.All(finding => finding.Severity != HostHealthSeverity.Error);

    public bool RemoteAccessDegraded => Findings.Any(finding =>
        string.Equals(finding.Code, "cloudflared-missing", StringComparison.Ordinal));

    public static HostStartupHealth Evaluate(HostRuntimeOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);

        var findings = new List<HostHealthFinding>();

        try
        {
            options.Validate();
        }
        catch (Exception ex) when (ex is InvalidOperationException or ArgumentException)
        {
            findings.Add(new HostHealthFinding("invalid-options", HostHealthSeverity.Error, ex.Message));
            return new HostStartupHealth(findings);
        }

        foreach (var directory in options.Paths.RequiredDirectories())
        {
            try
            {
                Directory.CreateDirectory(directory);
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or NotSupportedException)
            {
                findings.Add(new HostHealthFinding(
                    "path-unavailable",
                    HostHealthSeverity.Error,
                    $"Runtime directory is unavailable: {directory}. {ex.Message}"));
            }
        }

        if (options.RemoteAccessEnabled)
        {
            var cloudflared = HostDependencyProbe.ProbeExecutable(options.CloudflaredExecutable);
            if (!cloudflared.Available)
            {
                findings.Add(new HostHealthFinding(
                    "cloudflared-missing",
                    HostHealthSeverity.Warning,
                    "Remote access is enabled but cloudflared is unavailable. Local authority can still start."));
            }
        }

        if (findings.Count == 0)
        {
            findings.Add(new HostHealthFinding("healthy", HostHealthSeverity.Info, "Host prerequisites are available."));
        }

        return new HostStartupHealth(findings);
    }
}
