using System.Security.Cryptography;
using System.Text;

namespace InterLan.Server;

public static class RateLimitPartitionKeys
{
    public static string RemoteAddress(HttpContext context)
    {
        var address = context.Connection.RemoteIpAddress;
        return address is null ? "ip:unknown" : $"ip:{address.MapToIPv6()}";
    }

    public static string AuthenticatedClient(HttpContext context)
    {
        var authorization = context.Request.Headers.Authorization.ToString();
        if (!authorization.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase))
            return RemoteAddress(context);

        var token = authorization["Bearer ".Length..].Trim();
        if (string.IsNullOrWhiteSpace(token))
            return RemoteAddress(context);

        var digest = SHA256.HashData(Encoding.UTF8.GetBytes(token));
        return $"session:{Convert.ToHexString(digest)}";
    }
}
