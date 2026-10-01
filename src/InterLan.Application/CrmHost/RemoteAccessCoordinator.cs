namespace InterLan.Application.CrmHost;

public sealed class RemoteAccessCoordinator : IAsyncDisposable
{
    private readonly SemaphoreSlim _gate = new(1, 1);
    private readonly HostRuntimeOptions _options;
    private readonly ProcessSupervisor _supervisor;
    private readonly QuickTunnelLauncher _launcher;
    private readonly IHostNetworkStatus _network;
    private readonly TunnelRestartPolicy _restartPolicy;
    private readonly IHostLifecycleEventSink _events;
    private TunnelRuntimeState _state = TunnelRuntimeState.Disabled;
    private bool _remoteEnabled;
    private bool _disposed;

    public RemoteAccessCoordinator(
        HostRuntimeOptions options,
        ProcessSupervisor supervisor,
        IHostNetworkStatus network,
        IHostLifecycleEventSink events)
    {
        _options = options ?? throw new ArgumentNullException(nameof(options));
        _supervisor = supervisor ?? throw new ArgumentNullException(nameof(supervisor));
        _launcher = new QuickTunnelLauncher(supervisor);
        _network = network ?? throw new ArgumentNullException(nameof(network));
        _events = events ?? throw new ArgumentNullException(nameof(events));
        _restartPolicy = new TunnelRestartPolicy(
            options.MaxTunnelRestartAttempts,
            options.TunnelRestartBackoff);
        _network.AvailabilityChanged += OnNetworkAvailabilityChanged;
    }

    public TunnelRuntimeState State => Volatile.Read(ref _state);

    public async Task<TunnelRuntimeState> StartAsync(CancellationToken cancellationToken = default)
    {
        await _gate.WaitAsync(cancellationToken);
        try
        {
            ThrowIfDisposed();
            _remoteEnabled = true;

            if (_state.Phase == TunnelRuntimePhase.Connected)
            {
                return _state;
            }

            return await StartCoreAsync(restartAttempt: 0, cancellationToken);
        }
        finally
        {
            _gate.Release();
        }
    }

    public async Task<TunnelRuntimeState> RestartAsync(CancellationToken cancellationToken = default)
    {
        await _gate.WaitAsync(cancellationToken);
        try
        {
            ThrowIfDisposed();
            _remoteEnabled = true;
            Volatile.Write(ref _state, _state.Restarting(1, "Manual Quick Tunnel restart requested."));
            await StopProcessCoreAsync(cancellationToken);
            return await StartCoreAsync(restartAttempt: 1, cancellationToken);
        }
        finally
        {
            _gate.Release();
        }
    }

    public async Task<TunnelRuntimeState> StopAsync(CancellationToken cancellationToken = default)
    {
        await _gate.WaitAsync(cancellationToken);
        try
        {
            _remoteEnabled = false;
            Volatile.Write(ref _state, _state with
            {
                Phase = TunnelRuntimePhase.Stopping,
                PublicUri = null,
                ConnectedAt = null,
                Detail = "Remote access is stopping."
            });

            await StopProcessCoreAsync(cancellationToken);
            Volatile.Write(ref _state, TunnelRuntimeState.Disabled);
            return _state;
        }
        finally
        {
            _gate.Release();
        }
    }

    public async ValueTask DisposeAsync()
    {
        if (_disposed)
        {
            return;
        }

        _network.AvailabilityChanged -= OnNetworkAvailabilityChanged;
        await StopAsync();
        _disposed = true;
        _gate.Dispose();
    }

    private async Task<TunnelRuntimeState> StartCoreAsync(int restartAttempt, CancellationToken cancellationToken)
    {
        var location = CloudflaredLocator.Locate(_options.CloudflaredExecutable);
        if (!location.Available || location.ExecutablePath is null)
        {
            var degraded = _state.Degraded(location.Detail);
            Volatile.Write(ref _state, degraded);
            await PublishDegradedAsync(location.Detail, cancellationToken);
            return degraded;
        }

        if (!_network.IsNetworkAvailable)
        {
            const string detail = "Network is unavailable; local INTER-LAN authority remains available without a tunnel.";
            var degraded = _state.Degraded(detail);
            Volatile.Write(ref _state, degraded);
            await PublishDegradedAsync(detail, cancellationToken);
            return degraded;
        }

        Volatile.Write(ref _state, restartAttempt == 0
            ? TunnelRuntimeState.Starting()
            : _state.Restarting(restartAttempt, $"Starting Quick Tunnel restart attempt {restartAttempt}."));

        try
        {
            var handle = await _launcher.StartAsync(
                location.ExecutablePath,
                _options.LocalGatewayUri,
                _options.ChildStartupTimeout,
                cancellationToken);

            var connected = _state.Connected(handle.PublicUri, handle.ConnectedAt);
            Volatile.Write(ref _state, connected);

            await _events.PublishAsync(
                new HostLifecycleEvent(
                    HostLifecycleEventKind.RemoteAccessStarted,
                    DateTimeOffset.UtcNow,
                    "Quick Tunnel connected to the role-limited loopback gateway.",
                    handle.ProcessId,
                    CloudflaredQuickTunnelPlan.ComponentName),
                cancellationToken);

            _ = RecoverAfterExitAsync();
            return connected;
        }
        catch (Exception ex) when (ex is InvalidOperationException or TimeoutException or System.ComponentModel.Win32Exception)
        {
            var detail = $"Quick Tunnel start failed: {ex.Message}";
            var degraded = _state.Degraded(detail);
            Volatile.Write(ref _state, degraded);
            await PublishDegradedAsync(detail, cancellationToken);
            return degraded;
        }
    }

    private async Task RecoverAfterExitAsync()
    {
        var exit = await _supervisor.WaitForExitAsync(CloudflaredQuickTunnelPlan.ComponentName);

        await _gate.WaitAsync();
        try
        {
            if (!_remoteEnabled || _state.Phase is TunnelRuntimePhase.Disabled or TunnelRuntimePhase.Stopping)
            {
                return;
            }

            var reason = exit is null
                ? "Quick Tunnel process ownership disappeared."
                : $"Quick Tunnel process exited with code {exit.ExitCode}.";

            Volatile.Write(ref _state, _state.Degraded(reason));
            await PublishDegradedAsync(reason, CancellationToken.None);

            var completedAttempts = 0;
            while (_remoteEnabled)
            {
                var decision = _restartPolicy.Decide(completedAttempts, _network.IsNetworkAvailable);
                if (!decision.ShouldRestart)
                {
                    return;
                }

                Volatile.Write(ref _state, _state.Restarting(decision.Attempt, decision.Reason));
                if (decision.Delay > TimeSpan.Zero)
                {
                    await Task.Delay(decision.Delay);
                }

                var restarted = await StartCoreAsync(decision.Attempt, CancellationToken.None);
                if (restarted.Phase == TunnelRuntimePhase.Connected)
                {
                    return;
                }

                completedAttempts = decision.Attempt;
            }
        }
        finally
        {
            _gate.Release();
        }
    }

    private async Task StopProcessCoreAsync(CancellationToken cancellationToken)
    {
        await _supervisor.StopAsync(
            CloudflaredQuickTunnelPlan.ComponentName,
            _options.GracefulShutdownTimeout,
            cancellationToken);
    }

    private async Task PublishDegradedAsync(string detail, CancellationToken cancellationToken)
    {
        await _events.PublishAsync(
            new HostLifecycleEvent(
                HostLifecycleEventKind.RemoteAccessDegraded,
                DateTimeOffset.UtcNow,
                detail,
                Component: CloudflaredQuickTunnelPlan.ComponentName),
            cancellationToken);
    }

    private void OnNetworkAvailabilityChanged(object? sender, bool available)
    {
        if (_disposed || !_remoteEnabled)
        {
            return;
        }

        _ = HandleNetworkAvailabilityChangedAsync(available);
    }

    private async Task HandleNetworkAvailabilityChangedAsync(bool available)
    {
        await _gate.WaitAsync();
        try
        {
            if (!_remoteEnabled || _disposed)
            {
                return;
            }

            if (!available)
            {
                const string detail = "Network became unavailable; stopping remote transport while preserving local host authority.";
                Volatile.Write(ref _state, _state.Degraded(detail));
                await StopProcessCoreAsync(CancellationToken.None);
                await PublishDegradedAsync(detail, CancellationToken.None);
                return;
            }

            if (_state.Phase is TunnelRuntimePhase.Degraded or TunnelRuntimePhase.Failed)
            {
                await StartCoreAsync(restartAttempt: 1, CancellationToken.None);
            }
        }
        finally
        {
            _gate.Release();
        }
    }

    private void ThrowIfDisposed()
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
    }
}
