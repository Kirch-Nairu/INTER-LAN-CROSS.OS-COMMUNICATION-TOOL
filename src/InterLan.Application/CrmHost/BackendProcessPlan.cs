namespace InterLan.Application.CrmHost;

public static class BackendProcessPlan
{
    public const string ComponentName = "crm-backend";
    public const string HealthPath = "/health";

    public static ProcessLaunchSpec Create(
        string serverAssemblyPath,
        LinuxHostPaths paths,
        Uri backendUri,
        string dotnetExecutable = "dotnet")
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(serverAssemblyPath);
        ArgumentNullException.ThrowIfNull(paths);
        ArgumentNullException.ThrowIfNull(backendUri);
        ArgumentException.ThrowIfNullOrWhiteSpace(dotnetExecutable);

        if (!backendUri.IsLoopback ||
            !string.Equals(backendUri.Scheme, Uri.UriSchemeHttps, StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException("The canonical CRM backend process must bind HTTPS on loopback.");
        }

        var assembly = Path.GetFullPath(serverAssemblyPath);
        if (!File.Exists(assembly))
        {
            throw new FileNotFoundException("The INTER-LAN server assembly was not found.", assembly);
        }

        var port = backendUri.Port;
        if (port is <= 0 or > 65535)
        {
            throw new InvalidOperationException("The canonical CRM backend port is invalid.");
        }

        return new ProcessLaunchSpec(
            ComponentName,
            dotnetExecutable,
            [
                assembly,
                $"--InterLan:Server:BindAddress={backendUri.Host}",
                $"--InterLan:Server:Port={port}",
                "--InterLan:Server:DiscoveryEnabled=false",
                $"--InterLan:Server:StoragePath={paths.DataDirectory}"
            ],
            Path.GetDirectoryName(assembly),
            new Dictionary<string, string?>(StringComparer.Ordinal)
            {
                ["INTERLAN_DATA_DIR"] = paths.DataDirectory
            });
    }
}
