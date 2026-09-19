using System.Net.Security;
using System.Security.Cryptography;

namespace InterLan.Application;

public static class PinnedTransportFactory
{
    public static SocketsHttpHandler CreateHandler(ClientPairingState pairing)
    {
        ArgumentNullException.ThrowIfNull(pairing);

        var expectedFingerprint =
            Convert.FromHexString(pairing.PinnedCertificateSha256);

        return new SocketsHttpHandler
        {
            UseProxy = false,
            ConnectTimeout = TimeSpan.FromSeconds(5),
            SslOptions = new SslClientAuthenticationOptions
            {
                RemoteCertificateValidationCallback = (_, certificate, _, _) =>
                {
                    if (certificate is null)
                        return false;

                    var actual = SHA256.HashData(
                        certificate.GetRawCertData());

                    return actual.Length == expectedFingerprint.Length &&
                           CryptographicOperations.FixedTimeEquals(
                               actual,
                               expectedFingerprint);
                }
            }
        };
    }

    public static HttpClient CreateHttpClient(
        ClientPairingState pairing,
        TimeSpan? timeout = null)
    {
        var client = new HttpClient(CreateHandler(pairing))
        {
            BaseAddress = new Uri(pairing.ServerUri),
            Timeout = timeout ?? TimeSpan.FromSeconds(15)
        };

        return client;
    }
}
