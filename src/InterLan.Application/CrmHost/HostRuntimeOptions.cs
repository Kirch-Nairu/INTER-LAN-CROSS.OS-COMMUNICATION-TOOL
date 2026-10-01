namespace InterLan.Application.CrmHost;

public sealed record HostRuntimeOptions
{
    public required LinuxHostPaths Paths { get; init; }

    public Uri LocalGatewayUri { get; init; } = new("http://127.0.0.1:5080");

    public bool RemoteAccessEnabled { get; init; }

    public string CloudflaredExecutable { get; init; } = "cloudflared";

    public TimeSpan ChildStartupTimeout { get; init; } = TimeSpan.FromSeconds(20);

    public TimeSpan GracefulShutdownTimeout { get; init; } = TimeSpan.FromSeconds(10);

    public TimeSpan TunnelRestartBackoff { get; init; } = TimeSpan.FromSeconds(3);

    public int MaxTunnelRestartAttempts { get; init; } = 3;

    public void Validate()
    {
        if (!LocalGatewayUri.IsLoopback)
        {
            throw new InvalidOperationException("The CRM gateway must bind to loopback before remote tunneling.");
        }

        if (!string.Equals(LocalGatewayUri.Scheme, Uri.UriSchemeHttp, StringComparison.OrdinalIgnoreCase) &&
            !string.Equals(LocalGatewayUri.Scheme, Uri.UriSchemeHttps, StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException("The local CRM gateway must use HTTP or HTTPS.");
        }

        if (string.IsNullOrWhiteSpace(CloudflaredExecutable))
        {
            throw new InvalidOperationException("The cloudflared executable name or path is required.");
        }

        if (ChildStartupTimeout <= TimeSpan.Zero || GracefulShutdownTimeout <= TimeSpan.Zero)
        {
            throw new InvalidOperationException("Runtime timeouts must be positive.");
        }

        if (MaxTunnelRestartAttempts < 0)
        {
            throw new InvalidOperationException("Tunnel restart attempts cannot be negative.");
        }
    }
}
