namespace InterLan.Application.CrmHost;

public sealed class LinuxHostRuntime : IHostRuntimeOperations, IAsyncDisposable
{
    private readonly HostRuntimeOptions _options;
    private readonly string _serverAssemblyPath;
    private readonly IHostLifecycleEventSink _events;
    private readonly IHostNetworkStatus _network;
    private readonly bool _ownsNetwork;
    private readonly HostRuntimeCoordinator _coordinator = new();
    private readonly RuntimeStateStore _stateStore;
    private readonly LoopbackReadinessProbe _gatewayReadiness = new();
    private readonly SemaphoreSlim _gate = new(1, 1);
    private SingleInstanceLease? _lease;
    private ProcessSupervisor? _supervisor;
    private HostProcessSet? _processes;
    private RemoteAccessCoordinator? _remoteAccess;
    private bool _disposed;

    public LinuxHostRuntime(
        HostRuntimeOptions options,
        string serverAssemblyPath,
        IHostLifecycleEventSink events,
        IHostNetworkStatus? network = null)
    {
        _options = options ?? throw new ArgumentNullException(nameof(options));
        ArgumentException.ThrowIfNullOrWhiteSpace(serverAssemblyPath);
        _serverAssemblyPath = Path.GetFullPath(serverAssemblyPath);
        _events = events ?? throw new ArgumentNullException(nameof(events));
        _network = network ?? new HostNetworkMonitor();
        _ownsNetwork = network is null;
        _stateStore = new RuntimeStateStore(options.Paths.RuntimeStatePath);
    }

    public HostRuntimeSnapshot Snapshot => _coordinator.Snapshot;

    public async Task<HostRuntimeSnapshot> StartAsync(
        bool remoteAccess,
        CancellationToken cancellationToken = default)
    {
        await _gate.WaitAsync(cancellationToken);
        try
        {
            ThrowIfDisposed();

            if (Snapshot.Phase is HostRuntimePhase.Ready or HostRuntimePhase.Degraded)
            {
                return Snapshot;
            }

            var effectiveOptions = _options with { RemoteAccessEnabled = remoteAccess };
            var health = HostStartupHealth.Evaluate(effectiveOptions);
            if (!health.CanStartLocalHost)
            {
                throw new InvalidOperationException(string.Join(
                    "; ",
                    health.Findings
                        .Where(finding => finding.Severity == HostHealthSeverity.Error)
                        .Select(finding => finding.Message)));
            }

            await _events.PublishAsync(
                new HostLifecycleEvent(
                    HostLifecycleEventKind.StartRequested,
                    DateTimeOffset.UtcNow,
                    remoteAccess ? "Host start requested with remote access." : "Host start requested for local authority only."),
                cancellationToken);

            _lease = SingleInstanceLease.Acquire(effectiveOptions.Paths.LockFilePath);
            _coordinator.BeginStart(effectiveOptions.LocalGatewayUri, DateTimeOffset.UtcNow);

            await _events.PublishAsync(
                new HostLifecycleEvent(
                    HostLifecycleEventKind.LeaseAcquired,
                    DateTimeOffset.UtcNow,
                    "Canonical host single-instance lease acquired."),
                cancellationToken);

            _supervisor = new ProcessSupervisor();
            _processes = new HostProcessSet(
                _supervisor,
                effectiveOptions,
                _serverAssemblyPath,
                _events);

            try
            {
                await _processes.StartBackendAsync(cancellationToken);
                await WaitForBackendAsync(effectiveOptions, cancellationToken);

                await _processes.StartGatewayAsync(cancellationToken);
                await _gatewayReadiness.WaitUntilReadyAsync(
                    effectiveOptions.LocalGatewayUri,
                    GatewayProcessPlan.HealthPath,
                    effectiveOptions.ChildStartupTimeout,
                    cancellationToken);

                Uri? publicUri = null;
                if (remoteAccess)
                {
                    _remoteAccess = new RemoteAccessCoordinator(
                        effectiveOptions,
                        _supervisor,
                        _network,
                        _events);
                    var tunnel = await _remoteAccess.StartAsync(cancellationToken);
                    if (tunnel.Phase == TunnelRuntimePhase.Connected)
                    {
                        publicUri = tunnel.PublicUri;
                    }
                }

                var snapshot = _coordinator.MarkReady(
                    remoteAccess,
                    publicUri,
                    DateTimeOffset.UtcNow);

                var processIds = await _processes.SnapshotAsync(cancellationToken);
                await _stateStore.WriteAsync(snapshot, processIds, cancellationToken);

                await _events.PublishAsync(
                    new HostLifecycleEvent(
                        HostLifecycleEventKind.Ready,
                        DateTimeOffset.UtcNow,
                        snapshot.Detail ?? "INTER-LAN host is ready."),
                    cancellationToken);

                return snapshot;
            }
            catch (Exception ex)
            {
                _coordinator.MarkFaulted($"Host startup failed: {ex.Message}", DateTimeOffset.UtcNow);
                await CleanupAfterFailureAsync();
                throw;
            }
        }
        finally
        {
            _gate.Release();
        }
    }

    public async Task<HostRuntimeSnapshot> StopAsync(CancellationToken cancellationToken = default)
    {
        await _gate.WaitAsync(cancellationToken);
        try
        {
            if (Snapshot.Phase == HostRuntimePhase.Stopped)
            {
                return Snapshot;
            }

            _coordinator.BeginStop(DateTimeOffset.UtcNow);
            await _events.PublishAsync(
                new HostLifecycleEvent(
                    HostLifecycleEventKind.StopRequested,
                    DateTimeOffset.UtcNow,
                    "Orderly host shutdown requested."),
                cancellationToken);

            if (_remoteAccess is not null)
            {
                await _remoteAccess.DisposeAsync();
                _remoteAccess = null;
            }

            if (_processes is not null)
            {
                await _processes.StopAsync(cancellationToken);
                await _processes.DisposeAsync();
                _processes = null;
                _supervisor = null;
            }

            _stateStore.Delete();
            _lease?.Dispose();
            _lease = null;

            var stopped = _coordinator.MarkStopped(DateTimeOffset.UtcNow);
            await _events.PublishAsync(
                new HostLifecycleEvent(
                    HostLifecycleEventKind.Stopped,
                    DateTimeOffset.UtcNow,
                    "INTER-LAN host stopped and released its canonical lease."),
                cancellationToken);
            return stopped;
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

        await StopAsync();
        _disposed = true;
        if (_ownsNetwork && _network is IDisposable disposableNetwork)
        {
            disposableNetwork.Dispose();
        }

        _gate.Dispose();
    }

    private async Task WaitForBackendAsync(
        HostRuntimeOptions options,
        CancellationToken cancellationToken)
    {
        using var handler = new HttpClientHandler
        {
            ServerCertificateCustomValidationCallback = (request, _, _, _) =>
                request.RequestUri?.IsLoopback == true
        };

        var readiness = new LoopbackReadinessProbe(handler);
        await readiness.WaitUntilReadyAsync(
            options.LocalBackendUri,
            BackendProcessPlan.HealthPath,
            options.ChildStartupTimeout,
            cancellationToken);
    }

    private async Task CleanupAfterFailureAsync()
    {
        try
        {
            if (_remoteAccess is not null)
            {
                await _remoteAccess.DisposeAsync();
                _remoteAccess = null;
            }

            if (_processes is not null)
            {
                await _processes.StopAsync(CancellationToken.None);
                await _processes.DisposeAsync();
                _processes = null;
                _supervisor = null;
            }
        }
        finally
        {
            _stateStore.Delete();
            _lease?.Dispose();
            _lease = null;
            _coordinator.BeginStop(DateTimeOffset.UtcNow);
            _coordinator.MarkStopped(DateTimeOffset.UtcNow);
        }
    }

    private void ThrowIfDisposed()
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
    }
}
