using System.Net;
using System.Net.Sockets;

namespace InterLan.Testing;

public sealed class LoopbackPortLease : IDisposable
{
    private TcpListener? _listener;

    private LoopbackPortLease(TcpListener listener)
    {
        _listener = listener;
        Port = ((IPEndPoint)listener.LocalEndpoint).Port;
    }

    public int Port { get; }

    public static LoopbackPortLease Reserve()
    {
        var listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        return new LoopbackPortLease(listener);
    }

    public void Release()
    {
        var listener = Interlocked.Exchange(ref _listener, null);
        listener?.Stop();
    }

    public void Dispose() => Release();
}
