using System.Net;
using System.Net.Sockets;
using InterLan.Contracts;

namespace InterLan.Infrastructure;

public sealed class LanDiscoveryClient : IAsyncDisposable
{
    private readonly UdpClient _udp = new(AddressFamily.InterNetwork);

    public LanDiscoveryClient()
    {
        _udp.Client.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.ReuseAddress, true);
        _udp.Client.Bind(new IPEndPoint(IPAddress.Any, LanDiscoveryProtocol.MulticastPort));
        _udp.JoinMulticastGroup(IPAddress.Parse(LanDiscoveryProtocol.MulticastAddress));
    }

    public async IAsyncEnumerable<(DiscoveryAnnouncement Announcement, IPEndPoint Remote)> ListenAsync(
        [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        while (!cancellationToken.IsCancellationRequested)
        {
            UdpReceiveResult packet;
            try
            {
                packet = await _udp.ReceiveAsync(cancellationToken);
            }
            catch (OperationCanceledException)
            {
                yield break;
            }

            DiscoveryAnnouncement announcement;
            try
            {
                announcement = LanDiscoveryProtocol.Parse(packet.Buffer);
            }
            catch (Exception) when (!cancellationToken.IsCancellationRequested)
            {
                continue;
            }

            yield return (announcement, packet.RemoteEndPoint);
        }
    }

    public ValueTask DisposeAsync()
    {
        _udp.Dispose();
        return ValueTask.CompletedTask;
    }
}
