using InterLan.Application;
using InterLan.Contracts;
using InterLan.Domain;
using InterLan.Infrastructure;

namespace InterLan.Server;

public static class SystemEndpointMappings
{
    public static IEndpointRouteBuilder MapInterLanSystemEndpoints(
        this IEndpointRouteBuilder endpoints)
    {
        endpoints.MapGet("/health", () => Results.Ok(new
        {
            status = "ok",
            apiVersion = ApiContractVersion.Current,
            utc = DateTimeOffset.UtcNow
        }));

        endpoints.MapGet("/api/v1/server/info", async (
            IServerIdentityStore identities,
            ServerRuntimeSettings settings,
            ServerCertificateDescriptor certificate,
            CancellationToken cancellationToken) =>
        {
            var identity = await identities.GetAsync(cancellationToken);

            return Results.Ok(new
            {
                server = new ServerInfoResponse(
                    ApiContractVersion.Current,
                    identity is not null,
                    identity?.ServerId,
                    identity?.ServerName,
                    identity is null ? RuntimeMode.Unconfigured : RuntimeMode.ServerOwner,
                    NativeDesktopSupported: true,
                    WebClientSupported: true),
                httpsPort = settings.Port,
                certificateSha256 = certificate.Sha256Fingerprint
            });
        });

        return endpoints;
    }
}
