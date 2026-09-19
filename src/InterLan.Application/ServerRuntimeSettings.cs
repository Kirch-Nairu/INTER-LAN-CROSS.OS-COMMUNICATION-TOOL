namespace InterLan.Application;

public sealed record ServerRuntimeSettings(
    string BindAddress,
    int Port,
    bool DiscoveryEnabled,
    bool ClientApprovalRequired,
    string StoragePath)
{
    public static ServerRuntimeSettings CreateDefaults(string dataDirectory) =>
        new(
            "0.0.0.0",
            7443,
            DiscoveryEnabled: true,
            ClientApprovalRequired: true,
            Path.Combine(Path.GetFullPath(dataDirectory), "files"));

    public IReadOnlyList<string> Validate()
    {
        var errors = new List<string>();

        if (!System.Net.IPAddress.TryParse(BindAddress, out _))
            errors.Add("BindAddress must be an IP address.");

        if (Port is < 1 or > 65535)
            errors.Add("Port must be between 1 and 65535.");

        if (string.IsNullOrWhiteSpace(StoragePath))
            errors.Add("StoragePath is required.");

        return errors;
    }
}
