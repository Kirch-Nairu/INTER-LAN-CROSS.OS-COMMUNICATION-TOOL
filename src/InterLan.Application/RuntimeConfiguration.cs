using InterLan.Domain;

namespace InterLan.Application;

public sealed record RuntimeConfiguration(
    RuntimeMode Mode,
    ServerHostConfiguration? Server,
    ClientRuntimeConfiguration? Client)
{
    public static RuntimeConfiguration Unconfigured { get; } =
        new(RuntimeMode.Unconfigured, null, null);

    public IReadOnlyList<string> Validate()
    {
        var errors = new List<string>();

        switch (Mode)
        {
            case RuntimeMode.Unconfigured:
                if (Server is not null || Client is not null)
                {
                    errors.Add("Unconfigured mode cannot carry server or client authority.");
                }
                break;

            case RuntimeMode.ServerOwner:
                if (Server is null)
                {
                    errors.Add("ServerOwner mode requires server configuration.");
                }

                if (Client is not null)
                {
                    errors.Add("ServerOwner mode cannot carry client-only configuration.");
                }

                if (Server is not null)
                {
                    if (Server.ServerId == Guid.Empty) errors.Add("ServerId is required.");
                    if (Server.OwnerUserId == Guid.Empty) errors.Add("OwnerUserId is required.");
                    if (string.IsNullOrWhiteSpace(Server.ServerName)) errors.Add("ServerName is required.");
                    if (string.IsNullOrWhiteSpace(Server.DatabasePath)) errors.Add("DatabasePath is required.");
                    if (Server.Port is < 1 or > 65535) errors.Add("Server port is invalid.");
                }
                break;

            case RuntimeMode.Client:
                if (Client is null)
                {
                    errors.Add("Client mode requires client configuration.");
                }

                if (Server is not null)
                {
                    errors.Add("Client mode cannot carry server-owner configuration.");
                }

                if (Client is not null && !Uri.TryCreate(Client.ServerUri, UriKind.Absolute, out _))
                {
                    errors.Add("Client ServerUri must be an absolute URI.");
                }
                break;

            default:
                errors.Add("Unknown runtime mode.");
                break;
        }

        return errors;
    }
}

public sealed record ServerHostConfiguration(
    Guid ServerId,
    Guid OwnerUserId,
    string ServerName,
    string DatabasePath,
    string BindAddress,
    int Port,
    bool DiscoveryEnabled);

public sealed record ClientRuntimeConfiguration(
    string ServerUri,
    string? PinnedCertificateFingerprint,
    string DeviceName);
