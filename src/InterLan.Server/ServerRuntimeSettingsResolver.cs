using InterLan.Application;

namespace InterLan.Server;

public static class ServerRuntimeSettingsResolver
{
    public static ServerRuntimeSettings ApplyExplicitOverrides(
        ServerRuntimeSettings persisted,
        IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(persisted);
        ArgumentNullException.ThrowIfNull(configuration);

        var bindAddress = ReadString(configuration, "InterLan:Server:BindAddress")
            ?? persisted.BindAddress;

        var port = ReadInt(configuration, "InterLan:Server:Port")
            ?? persisted.Port;

        var discoveryEnabled = ReadBool(configuration, "InterLan:Server:DiscoveryEnabled")
            ?? persisted.DiscoveryEnabled;

        var approvalRequired = ReadBool(configuration, "InterLan:Server:ClientApprovalRequired")
            ?? persisted.ClientApprovalRequired;

        var storagePath = ReadString(configuration, "InterLan:Server:StoragePath")
            ?? persisted.StoragePath;

        var resolved = new ServerRuntimeSettings(
            bindAddress,
            port,
            discoveryEnabled,
            approvalRequired,
            Path.GetFullPath(storagePath));

        var errors = resolved.Validate();
        if (errors.Count > 0)
            throw new InvalidOperationException(
                $"Resolved server settings are invalid: {string.Join("; ", errors)}");

        return resolved;
    }

    private static string? ReadString(
        IConfiguration configuration,
        string key)
    {
        var value = configuration[key];
        return string.IsNullOrWhiteSpace(value) ? null : value.Trim();
    }

    private static int? ReadInt(
        IConfiguration configuration,
        string key)
    {
        var value = ReadString(configuration, key);
        if (value is null)
            return null;

        if (!int.TryParse(value, out var parsed))
            throw new InvalidOperationException($"{key} must be an integer.");

        return parsed;
    }

    private static bool? ReadBool(
        IConfiguration configuration,
        string key)
    {
        var value = ReadString(configuration, key);
        if (value is null)
            return null;

        if (!bool.TryParse(value, out var parsed))
            throw new InvalidOperationException($"{key} must be true or false.");

        return parsed;
    }
}
