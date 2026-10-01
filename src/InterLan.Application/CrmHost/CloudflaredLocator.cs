namespace InterLan.Application.CrmHost;

public sealed record CloudflaredLocation(
    bool Available,
    string? ExecutablePath,
    string Detail);

public static class CloudflaredLocator
{
    public static CloudflaredLocation Locate(string configuredExecutable, string? pathVariable = null)
    {
        var result = HostDependencyProbe.ProbeExecutable(configuredExecutable, pathVariable);
        if (!result.Available)
        {
            return new CloudflaredLocation(
                false,
                null,
                "cloudflared is unavailable; remote access must remain degraded while local authority continues.");
        }

        return new CloudflaredLocation(
            true,
            result.ResolvedPath ?? configuredExecutable,
            "cloudflared executable resolved without invoking a shell.");
    }
}
