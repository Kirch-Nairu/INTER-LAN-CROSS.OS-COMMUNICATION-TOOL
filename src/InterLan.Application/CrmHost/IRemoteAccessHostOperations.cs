namespace InterLan.Application.CrmHost;

public interface IRemoteAccessHostOperations
{
    TunnelRuntimeState RemoteAccessState { get; }

    Task<TunnelRuntimeState> EnableRemoteAccessAsync(CancellationToken cancellationToken = default);

    Task<TunnelRuntimeState> RestartRemoteAccessAsync(CancellationToken cancellationToken = default);

    Task<TunnelRuntimeState> DisableRemoteAccessAsync(CancellationToken cancellationToken = default);
}
