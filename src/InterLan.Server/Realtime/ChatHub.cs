using InterLan.Application;
using InterLan.Contracts;
using InterLan.Infrastructure;
using Microsoft.AspNetCore.SignalR;

namespace InterLan.Server.Realtime;

public sealed class ChatHub(EnrollmentStore enrollment) : Hub
{
    public static string UserGroup(Guid userId) => $"user:{userId:D}";

    public override async Task OnConnectedAsync()
    {
        var token = Context.GetHttpContext()?.Request.Query["access_token"].ToString();
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
        await Groups.AddToGroupAsync(Context.ConnectionId, UserGroup(principal.UserId));
        await base.OnConnectedAsync();
    }

    public ControlPong Ping() =>
        new(ApiContractVersion.Current, DateTimeOffset.UtcNow, "ok");
}
