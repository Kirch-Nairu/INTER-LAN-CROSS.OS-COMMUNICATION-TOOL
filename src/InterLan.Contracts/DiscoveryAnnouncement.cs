using System.Text.Json;
using System.Text.Json.Serialization;

namespace InterLan.Contracts;

public sealed record DiscoveryAnnouncement(
    string Protocol,
    Guid ServerId,
    string ServerName,
    int HttpsPort,
    string CertificateSha256);

public static class LanDiscoveryProtocol
{
    public const string ProtocolId = "interlan-v1";
    public const string MulticastAddress = "239.255.72.77";
    public const int MulticastPort = 47777;

    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        PropertyNameCaseInsensitive = true
    };

    public static byte[] Serialize(DiscoveryAnnouncement announcement)
    {
        Validate(announcement);
        return JsonSerializer.SerializeToUtf8Bytes(announcement, JsonOptions);
    }

    public static DiscoveryAnnouncement Parse(ReadOnlySpan<byte> payload)
    {
        var announcement = JsonSerializer.Deserialize<DiscoveryAnnouncement>(payload, JsonOptions)
            ?? throw new InvalidDataException("Discovery announcement is empty.");
        Validate(announcement);
        return announcement;
    }

    private static void Validate(DiscoveryAnnouncement announcement)
    {
        if (!string.Equals(announcement.Protocol, ProtocolId, StringComparison.Ordinal))
            throw new InvalidDataException("Unsupported discovery protocol.");
        if (announcement.ServerId == Guid.Empty)
            throw new InvalidDataException("ServerId is required.");
        if (string.IsNullOrWhiteSpace(announcement.ServerName) || announcement.ServerName.Length > 120)
            throw new InvalidDataException("ServerName is invalid.");
        if (announcement.HttpsPort is < 1 or > 65535)
            throw new InvalidDataException("HTTPS port is invalid.");
        if (announcement.CertificateSha256.Length != 64 ||
            !announcement.CertificateSha256.All(Uri.IsHexDigit))
            throw new InvalidDataException("Certificate fingerprint must be 64 hexadecimal characters.");
    }
}
