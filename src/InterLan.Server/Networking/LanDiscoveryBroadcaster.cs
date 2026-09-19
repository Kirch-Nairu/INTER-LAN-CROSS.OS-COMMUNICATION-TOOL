using System.Net;
using System.Net.Sockets;
using InterLan.Application;
using InterLan.Contracts;
using InterLan.Infrastructure;

namespace InterLan.Server.Networking;

public sealed class LanDiscoveryBroadcaster(
    IServerIdentityStore identities,
    EnrollmentStore enrollment,
    ServerCertificateDescriptor certificate,
    IConfiguration configuration,
    ILogger<LanDiscoveryBroadcaster> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var udp = new UdpClient(AddressFamily.InterNetwork);
        udp.MulticastLoopback = false;
        var endpoint = new IPEndPoint(
            IPAddress.Parse(LanDiscoveryProtocol.MulticastAddress),
            LanDiscoveryProtocol.MulticastPort);

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                var identity = await identities.GetAsync(stoppingToken);
                var discoveryEnabled = await enrollment.IsDiscoveryEnabledAsync(stoppingToken);
                if (identity is not null && discoveryEnabled)
                {
                    var port = configuration.GetValue("InterLan:Server:Port", 7443);
                    var announcement = new DiscoveryAnnouncement(
                        LanDiscoveryProtocol.ProtocolId,
                        identity.ServerId,
                        identity.ServerName,
                        port,
                        certificate.Sha256Fingerprint);

                    var payload = LanDiscoveryProtocol.Serialize(announcement);
                    await udp.SendAsync(payload, endpoint, stoppingToken);
                }
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception exception)
            {
                logger.LogWarning(exception, "LAN discovery advertisement failed.");
            }

            await Task.Delay(TimeSpan.FromSeconds(3), stoppingToken);
        }
    }
}
