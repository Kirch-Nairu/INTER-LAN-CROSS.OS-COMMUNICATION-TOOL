namespace InterLan.Application.CrmHost;

public sealed class HostRuntimeCoordinator
{
    private readonly object _gate = new();
    private readonly HostLifecycleStateMachine _stateMachine = new();
    private HostRuntimeSnapshot _snapshot = HostRuntimeSnapshot.Stopped(DateTimeOffset.UtcNow);

    public HostRuntimeSnapshot Snapshot
    {
        get
        {
            lock (_gate)
            {
                return _snapshot;
            }
        }
    }

    public HostRuntimeSnapshot BeginStart(Uri localGatewayUri, DateTimeOffset now)
    {
        ArgumentNullException.ThrowIfNull(localGatewayUri);
        _stateMachine.TransitionTo(HostRuntimePhase.Starting);

        lock (_gate)
        {
            _snapshot = new HostRuntimeSnapshot(
                HostRuntimePhase.Starting,
                RuntimeComponentState.Starting,
                RuntimeComponentState.Starting,
                RuntimeComponentState.Starting,
                RuntimeComponentState.Starting,
                RuntimeComponentState.Stopped,
                localGatewayUri,
                null,
                now,
                "Host startup is in progress.");
            return _snapshot;
        }
    }

    public HostRuntimeSnapshot MarkReady(bool tunnelRequired, Uri? publicGatewayUri, DateTimeOffset now)
    {
        var degraded = tunnelRequired && publicGatewayUri is null;
        _stateMachine.TransitionTo(degraded ? HostRuntimePhase.Degraded : HostRuntimePhase.Ready);

        lock (_gate)
        {
            _snapshot = _snapshot with
            {
                Phase = degraded ? HostRuntimePhase.Degraded : HostRuntimePhase.Ready,
                Database = RuntimeComponentState.Ready,
                Backend = RuntimeComponentState.Ready,
                Gateway = RuntimeComponentState.Ready,
                NativeControl = RuntimeComponentState.Ready,
                Tunnel = tunnelRequired
                    ? publicGatewayUri is null ? RuntimeComponentState.Degraded : RuntimeComponentState.Ready
                    : RuntimeComponentState.Stopped,
                PublicGatewayUri = publicGatewayUri,
                UpdatedAt = now,
                Detail = degraded
                    ? "Local authority is ready; remote access is degraded."
                    : "Host runtime is ready."
            };
            return _snapshot;
        }
    }

    public HostRuntimeSnapshot MarkFaulted(string detail, DateTimeOffset now)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(detail);
        if (_stateMachine.Phase != HostRuntimePhase.Faulted)
        {
            _stateMachine.TransitionTo(HostRuntimePhase.Faulted);
        }

        lock (_gate)
        {
            _snapshot = _snapshot with
            {
                Phase = HostRuntimePhase.Faulted,
                Database = ToFailedIfStarting(_snapshot.Database),
                Backend = ToFailedIfStarting(_snapshot.Backend),
                Gateway = ToFailedIfStarting(_snapshot.Gateway),
                NativeControl = ToFailedIfStarting(_snapshot.NativeControl),
                Tunnel = ToFailedIfStarting(_snapshot.Tunnel),
                PublicGatewayUri = null,
                UpdatedAt = now,
                Detail = detail
            };
            return _snapshot;
        }
    }

    public HostRuntimeSnapshot MarkTunnelState(RuntimeComponentState state, Uri? publicGatewayUri, string detail, DateTimeOffset now)
    {
        if (state == RuntimeComponentState.Ready && publicGatewayUri is null)
        {
            throw new InvalidOperationException("A ready tunnel must have a public URL.");
        }

        lock (_gate)
        {
            var phase = _stateMachine.Phase;
            if (phase is HostRuntimePhase.Ready && state is RuntimeComponentState.Degraded or RuntimeComponentState.Failed)
            {
                _stateMachine.TransitionTo(HostRuntimePhase.Degraded);
                phase = HostRuntimePhase.Degraded;
            }
            else if (phase == HostRuntimePhase.Degraded && state == RuntimeComponentState.Ready)
            {
                _stateMachine.TransitionTo(HostRuntimePhase.Ready);
                phase = HostRuntimePhase.Ready;
            }

            _snapshot = _snapshot with
            {
                Phase = phase,
                Tunnel = state,
                PublicGatewayUri = publicGatewayUri,
                UpdatedAt = now,
                Detail = detail
            };
            return _snapshot;
        }
    }

    public HostRuntimeSnapshot BeginStop(DateTimeOffset now)
    {
        var phase = _stateMachine.Phase;
        if (phase == HostRuntimePhase.Stopped)
        {
            return Snapshot;
        }

        _stateMachine.TransitionTo(HostRuntimePhase.Stopping);
        lock (_gate)
        {
            _snapshot = _snapshot with
            {
                Phase = HostRuntimePhase.Stopping,
                UpdatedAt = now,
                Detail = "Orderly shutdown is in progress."
            };
            return _snapshot;
        }
    }

    public HostRuntimeSnapshot MarkStopped(DateTimeOffset now)
    {
        if (_stateMachine.Phase != HostRuntimePhase.Stopped)
        {
            _stateMachine.TransitionTo(HostRuntimePhase.Stopped);
        }

        lock (_gate)
        {
            _snapshot = HostRuntimeSnapshot.Stopped(now);
            return _snapshot;
        }
    }

    private static RuntimeComponentState ToFailedIfStarting(RuntimeComponentState state) =>
        state == RuntimeComponentState.Starting ? RuntimeComponentState.Failed : state;
}
