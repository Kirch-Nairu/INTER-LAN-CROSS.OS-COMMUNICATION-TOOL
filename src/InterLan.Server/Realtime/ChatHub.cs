using InterLan.Application;
using InterLan.Contracts;
using InterLan.Infrastructure;
using Microsoft.AspNetCore.SignalR;

namespace InterLan.Server.Realtime;

public sealed class ChatHub(
    EnrollmentStore enrollment,
    RealtimeConnectionRegistry connections) : Hub
{
    private const string LeaseKey = "interlan-realtime-lease";

    public static string UserGroup(Guid userId) => $"user:{userId:D}";

    public override async Task OnConnectedAsync()
    {
        var http = Context.GetHttpContext();
        var token = ExtractBearerToken(http);

        if (string.IsNullOrWhiteSpace(token))
        {
            Context.Abort();
            return;
        }

        var principal = await enrollment.ValidateSessionAsync(token, Context.ConnectionAborted);
        if (principal is null)
        {
            Context.Abort();
            return;
        }

        Context.Items["principal"] = principal;
        Context.Items[LeaseKey] = connections.Register(
            principal,
            Context.ConnectionId,
            Context.Abort);

        await Groups.AddToGroupAsync(Context.ConnectionId, UserGroup(principal.UserId));
        await base.OnConnectedAsync();
    }

    public override async Task OnDisconnectedAsync(Exception? exception)
    {
        if (Context.Items.TryGetValue(LeaseKey, out var value) && value is IDisposable lease)
        {
            lease.Dispose();
            Context.Items.Remove(LeaseKey);
        }

        await base.OnDisconnectedAsync(exception);
    }

    public ControlPong Ping() =>
        new(ApiContractVersion.Current, DateTimeOffset.UtcNow, "ok");

    private static string? ExtractBearerToken(HttpContext? context)
    {
        if (context is null)
            return null;

        var authorization = context.Request.Headers.Authorization.ToString();
        if (authorization.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase))
        {
            var headerToken = authorization["Bearer ".Length..].Trim();
            if (!string.IsNullOrWhiteSpace(headerToken))
                return headerToken;
        }

        // Browser WebSocket transports may be forced to use the standard SignalR
        // access_token query parameter. Native clients should prefer Authorization.
        var queryToken = context.Request.Query["access_token"].ToString();
        return string.IsNullOrWhiteSpace(queryToken) ? null : queryToken;
    }
}
