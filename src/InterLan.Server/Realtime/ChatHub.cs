using InterLan.Application;
using InterLan.Contracts;
using InterLan.Infrastructure;
using Microsoft.AspNetCore.SignalR;

namespace InterLan.Server.Realtime;

public sealed class ChatHub(
    EnrollmentStore enrollment,
    ChatStore chat,
    RealtimeConnectionRegistry connections,
    RealtimeTicketStore tickets) : Hub
{
    private const string LeaseKey = "interlan-realtime-lease";

    public const string AuthenticatedGroup = "authenticated";
    public static string UserGroup(Guid userId) => $"user:{userId:D}";

    public override async Task OnConnectedAsync()
    {
        var principal = await ResolvePrincipalAsync(Context.GetHttpContext());

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

        await Groups.AddToGroupAsync(
            Context.ConnectionId,
            UserGroup(principal.UserId));
        await Groups.AddToGroupAsync(
            Context.ConnectionId,
            AuthenticatedGroup);

        await Clients.GroupExcept(
                AuthenticatedGroup,
                new[] { Context.ConnectionId })
            .SendAsync(
                "PresenceChanged",
                new UserPresenceResponse(
                    principal.UserId,
                    IsOnline: true,
                    connections.GetUserConnectionCount(principal.UserId),
                    DateTimeOffset.UtcNow),
                Context.ConnectionAborted);

        await base.OnConnectedAsync();
    }

    public override async Task OnDisconnectedAsync(Exception? exception)
    {
        var principal = Context.Items.TryGetValue("principal", out var principalValue)
            ? principalValue as SessionPrincipal
            : null;

        if (Context.Items.TryGetValue(LeaseKey, out var value) && value is IDisposable lease)
        {
            lease.Dispose();
            Context.Items.Remove(LeaseKey);
        }

        if (principal is not null)
        {
            // Typing state is intentionally ephemeral. Disconnect implicitly clears
            // any local typing indicator maintained by recipients.
            await Clients.GroupExcept(
                    AuthenticatedGroup,
                    new[] { Context.ConnectionId })
                .SendAsync(
                    "PresenceChanged",
                    new UserPresenceResponse(
                        principal.UserId,
                        connections.IsUserOnline(principal.UserId),
                        connections.GetUserConnectionCount(principal.UserId),
                        DateTimeOffset.UtcNow));
        }

        await base.OnDisconnectedAsync(exception);
    }

    public async Task SetTyping(
        Guid conversationId,
        bool isTyping)
    {
        if (!Context.Items.TryGetValue("principal", out var value) ||
            value is not SessionPrincipal principal)
        {
            throw new HubException("Authenticated session is required.");
        }

        IReadOnlyList<Guid> members;
        try
        {
            members = await chat.GetDirectMemberIdsAsync(
                principal.UserId,
                conversationId,
                Context.ConnectionAborted);
        }
        catch (UnauthorizedAccessException)
        {
            throw new HubException("Direct conversation access denied.");
        }

        var payload = new TypingIndicatorResponse(
            conversationId,
            principal.UserId,
            isTyping,
            DateTimeOffset.UtcNow);

        foreach (var memberId in members.Where(id => id != principal.UserId))
        {
            await Clients.Group(UserGroup(memberId))
                .SendAsync(
                    "TypingChanged",
                    payload,
                    Context.ConnectionAborted);
        }
    }

    public ControlPong Ping() =>
        new(ApiContractVersion.Current, DateTimeOffset.UtcNow, "ok");

    private async Task<SessionPrincipal?> ResolvePrincipalAsync(HttpContext? context)
    {
        if (context is null)
            return null;

        var authorization = context.Request.Headers.Authorization.ToString();
        if (authorization.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase))
        {
            var bearerToken = authorization["Bearer ".Length..].Trim();
            if (!string.IsNullOrWhiteSpace(bearerToken))
            {
                return await enrollment.ValidateSessionAsync(
                    bearerToken,
                    Context.ConnectionAborted);
            }
        }

        var realtimeTicket = context.Request.Query["ticket"].ToString();
        var ticketPrincipal = tickets.Consume(realtimeTicket);
        if (ticketPrincipal is null)
            return null;

        return await enrollment.ValidateSessionByIdAsync(
            ticketPrincipal.SessionId,
            Context.ConnectionAborted);
    }
}
