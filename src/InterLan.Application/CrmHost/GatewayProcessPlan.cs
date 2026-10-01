namespace InterLan.Application.CrmHost;

public static class GatewayProcessPlan
{
    public const string ComponentName = "crm-gateway";
    public const string ModeArgument = "--crm-gateway";
    public const string HealthPath = "/crm/gateway/health";

    public static ProcessLaunchSpec Create(
        string serverAssemblyPath,
        LinuxHostPaths paths,
        Uri localGatewayUri,
        string dotnetExecutable = "dotnet")
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(serverAssemblyPath);
        ArgumentNullException.ThrowIfNull(paths);
        ArgumentNullException.ThrowIfNull(localGatewayUri);
        ArgumentException.ThrowIfNullOrWhiteSpace(dotnetExecutable);

        if (!localGatewayUri.IsLoopback)
        {
            throw new InvalidOperationException("The CRM gateway process may only be planned for a loopback address.");
        }

        var assembly = Path.GetFullPath(serverAssemblyPath);
        if (!File.Exists(assembly))
        {
            throw new FileNotFoundException("The INTER-LAN server assembly was not found.", assembly);
        }

        var port = localGatewayUri.Port;
        if (port is <= 0 or > 65535)
        {
            throw new InvalidOperationException("The CRM gateway port is invalid.");
        }

        return new ProcessLaunchSpec(
            ComponentName,
            dotnetExecutable,
            [
                assembly,
                ModeArgument,
                $"--CrmGateway:Port={port}"
            ],
            Path.GetDirectoryName(assembly),
            new Dictionary<string, string?>(StringComparer.Ordinal)
            {
                ["INTERLAN_DATA_DIR"] = paths.DataDirectory
            });
    }
}
