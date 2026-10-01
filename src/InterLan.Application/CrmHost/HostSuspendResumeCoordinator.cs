namespace InterLan.Application.CrmHost;

public enum HostPowerState
{
    Active = 0,
    Suspending = 1,
    Suspended = 2,
    Resuming = 3
}

public sealed class HostSuspendResumeCoordinator
{
    private readonly IHostRuntimeOperations _host;
    private readonly IRemoteAccessHostOperations _remote;
    private readonly IHostLifecycleEventSink _events;
    private readonly SemaphoreSlim _gate = new(1, 1);
    private bool _hostWasRunning;
    private bool _restoreRemoteAccess;
    private HostPowerState _state = HostPowerState.Active;

    public HostSuspendResumeCoordinator(
        IHostRuntimeOperations host,
        IRemoteAccessHostOperations remote,
        IHostLifecycleEventSink events)
    {
        _host = host ?? throw new ArgumentNullException(nameof(host));
        _remote = remote ?? throw new ArgumentNullException(nameof(remote));
        _events = events ?? throw new ArgumentNullException(nameof(events));
    }

    public HostPowerState State => Volatile.Read(ref _state);

    public async Task SuspendAsync(CancellationToken cancellationToken = default)
    {
        await _gate.WaitAsync(cancellationToken);
        try
        {
            if (_state is HostPowerState.Suspending or HostPowerState.Suspended)
            {
                return;
            }

            Volatile.Write(ref _state, HostPowerState.Suspending);
            _hostWasRunning = _host.Snapshot.Phase is HostRuntimePhase.Ready or HostRuntimePhase.Degraded;
            _restoreRemoteAccess = _remote.RemoteAccessState.Phase is not TunnelRuntimePhase.Disabled;

            await _events.PublishAsync(
                new HostLifecycleEvent(
                    HostLifecycleEventKind.SuspendRequested,
                    DateTimeOffset.UtcNow,
                    "Host suspend detected; ephemeral remote transport will be dropped before sleep."),
                cancellationToken);

            if (_hostWasRunning && _restoreRemoteAccess)
            {
                await _remote.DisableRemoteAccessAsync(cancellationToken);
            }

            Volatile.Write(ref _state, HostPowerState.Suspended);
            await _events.PublishAsync(
                new HostLifecycleEvent(
                    HostLifecycleEventKind.Suspended,
                    DateTimeOffset.UtcNow,
                    "Host is suspend-safe; canonical local state remains on disk and no Quick Tunnel URL is trusted."),
                cancellationToken);
        }
        finally
        {
            _gate.Release();
        }
    }

    public async Task ResumeAsync(CancellationToken cancellationToken = default)
    {
        await _gate.WaitAsync(cancellationToken);
        try
        {
            if (_state != HostPowerState.Suspended)
            {
                return;
            }

            Volatile.Write(ref _state, HostPowerState.Resuming);
            await _events.PublishAsync(
                new HostLifecycleEvent(
                    HostLifecycleEventKind.ResumeRequested,
                    DateTimeOffset.UtcNow,
                    "Host resume detected; runtime and remote transport will be reconciled."),
                cancellationToken);

            if (_hostWasRunning && _host.Snapshot.Phase == HostRuntimePhase.Stopped)
            {
                await _host.StartAsync(_restoreRemoteAccess, cancellationToken);
            }
            else if (_hostWasRunning && _restoreRemoteAccess)
            {
                await _remote.EnableRemoteAccessAsync(cancellationToken);
            }

            _hostWasRunning = false;
            _restoreRemoteAccess = false;
            Volatile.Write(ref _state, HostPowerState.Active);

            await _events.PublishAsync(
                new HostLifecycleEvent(
                    HostLifecycleEventKind.Resumed,
                    DateTimeOffset.UtcNow,
                    "Host resume reconciliation completed; any remote URL is newly acquired."),
                cancellationToken);
        }
        finally
        {
            _gate.Release();
        }
    }
}
