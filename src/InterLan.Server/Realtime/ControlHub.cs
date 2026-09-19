using InterLan.Contracts;
using Microsoft.AspNetCore.SignalR;

namespace InterLan.Server.Realtime;

public sealed class ControlHub : Hub
{
    public ControlPong Ping() =>
        new(ApiContractVersion.Current, DateTimeOffset.UtcNow, "ok");
}
