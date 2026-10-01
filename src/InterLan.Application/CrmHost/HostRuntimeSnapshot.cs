namespace InterLan.Application.CrmHost;

public sealed record HostRuntimeSnapshot(
    HostRuntimePhase Phase,
    RuntimeComponentState Database,
    RuntimeComponentState Backend,
    RuntimeComponentState Gateway,
    RuntimeComponentState NativeControl,
    RuntimeComponentState Tunnel,
    Uri? LocalGatewayUri,
    Uri? PublicGatewayUri,
    DateTimeOffset UpdatedAt,
    string? Detail = null)
{
    public bool IsLocallyOperational =>
        Backend == RuntimeComponentState.Ready &&
        Gateway == RuntimeComponentState.Ready &&
        NativeControl == RuntimeComponentState.Ready;

    public bool IsRemoteAccessOperational =>
        IsLocallyOperational &&
        Tunnel == RuntimeComponentState.Ready &&
        PublicGatewayUri is not null;

    public static HostRuntimeSnapshot Stopped(DateTimeOffset now) => new(
        HostRuntimePhase.Stopped,
        RuntimeComponentState.Stopped,
        RuntimeComponentState.Stopped,
        RuntimeComponentState.Stopped,
        RuntimeComponentState.Stopped,
        RuntimeComponentState.Stopped,
        null,
        null,
        now);
}
