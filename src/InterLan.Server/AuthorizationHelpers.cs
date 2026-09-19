using InterLan.Application;
using InterLan.Infrastructure;

namespace InterLan.Server;

public static class AuthorizationHelpers
{
    public static async Task<SessionPrincipal?> GetPrincipalAsync(
        HttpContext context,
        EnrollmentStore enrollment,
        CancellationToken cancellationToken)
    {
        var header = context.Request.Headers.Authorization.ToString();
        if (!header.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase))
            return null;

        var token = header["Bearer ".Length..].Trim();
        if (string.IsNullOrWhiteSpace(token))
            return null;

        return await enrollment.ValidateSessionAsync(token, cancellationToken);
    }

    public static async Task<SessionPrincipal> RequireAuthenticatedAsync(
        HttpContext context,
        EnrollmentStore enrollment,
        CancellationToken cancellationToken)
    {
        return await GetPrincipalAsync(context, enrollment, cancellationToken)
            ?? throw new UnauthorizedAccessException("Authentication required.");
    }

    public static async Task<SessionPrincipal> RequireOwnerAsync(
        HttpContext context,
        EnrollmentStore enrollment,
        CancellationToken cancellationToken)
    {
        var principal = await RequireAuthenticatedAsync(context, enrollment, cancellationToken);

        if (!string.Equals(principal.Role, "OWNER", StringComparison.Ordinal))
            throw new UnauthorizedAccessException("Server owner authority required.");

        return principal;
    }
}
