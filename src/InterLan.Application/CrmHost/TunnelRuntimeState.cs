namespace InterLan.Application.CrmHost;

public enum TunnelRuntimePhase
{
    Disabled = 0,
    Starting = 1,
    Connected = 2,
    Degraded = 3,
    Restarting = 4,
    Stopping = 5,
    Failed = 6
}

public sealed record TunnelRuntimeState(
    TunnelRuntimePhase Phase,
    Uri? PublicUri,
    DateTimeOffset? ConnectedAt,
    int RestartAttempt,
    string? Detail)
{
    public static TunnelRuntimeState Disabled { get; } =
        new(TunnelRuntimePhase.Disabled, null, null, 0, "Remote access is disabled.");

    public static TunnelRuntimeState Starting(string? detail = null) =>
        new(TunnelRuntimePhase.Starting, null, null, 0, detail ?? "Quick Tunnel is starting.");

    public TunnelRuntimeState Connected(Uri publicUri, DateTimeOffset now) =>
        this with
        {
            Phase = TunnelRuntimePhase.Connected,
            PublicUri = publicUri,
            ConnectedAt = now,
            RestartAttempt = 0,
            Detail = "Quick Tunnel is connected."
        };

    public TunnelRuntimeState Degraded(string detail) =>
        this with
        {
            Phase = TunnelRuntimePhase.Degraded,
            PublicUri = null,
            ConnectedAt = null,
            Detail = detail
        };

    public TunnelRuntimeState Restarting(int attempt, string detail) =>
        this with
        {
            Phase = TunnelRuntimePhase.Restarting,
            PublicUri = null,
            ConnectedAt = null,
            RestartAttempt = attempt,
            Detail = detail
        };
}
