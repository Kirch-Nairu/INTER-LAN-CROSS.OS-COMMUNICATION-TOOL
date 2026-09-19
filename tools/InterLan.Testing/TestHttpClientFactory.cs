using System.Net.Security;

namespace InterLan.Testing;

public static class TestHttpClientFactory
{
    public static HttpClient CreateLoopback(
        TimeSpan? timeout = null)
    {
        var handler = new SocketsHttpHandler
        {
            UseProxy = false,
            ConnectTimeout = TimeSpan.FromSeconds(2),
            SslOptions = new SslClientAuthenticationOptions
            {
                RemoteCertificateValidationCallback = (_, _, _, _) => true
            }
        };

        return new HttpClient(handler)
        {
            Timeout = timeout ?? TimeSpan.FromSeconds(5)
        };
    }
}
