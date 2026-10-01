namespace InterLan.Application.CrmHost;

public static class CloudflaredQuickTunnelPlan
{
    public const string ComponentName = "cloudflared-quick-tunnel";

    public static ProcessLaunchSpec Create(string executablePath, Uri localGatewayUri)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(executablePath);
        ArgumentNullException.ThrowIfNull(localGatewayUri);

        if (!localGatewayUri.IsLoopback)
        {
            throw new InvalidOperationException("Quick Tunnel origin must be the local loopback CRM gateway.");
        }

        if (localGatewayUri.Scheme is not ("http" or "https"))
        {
            throw new InvalidOperationException("Quick Tunnel origin must use HTTP or HTTPS.");
        }

        return new ProcessLaunchSpec(
            ComponentName,
            executablePath,
            [
                "tunnel",
                "--no-autoupdate",
                "--url",
                localGatewayUri.AbsoluteUri.TrimEnd('/')
            ]);
    }
}
