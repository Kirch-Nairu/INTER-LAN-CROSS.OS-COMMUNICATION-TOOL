namespace InterLan.Application.CrmHost;

public sealed class HostProcessSet : IAsyncDisposable
{
    private readonly ProcessSupervisor _supervisor;
    private readonly HostRuntimeOptions _options;
    private readonly string _serverAssemblyPath;
    private readonly IHostLifecycleEventSink _events;

    public HostProcessSet(
        ProcessSupervisor supervisor,
        HostRuntimeOptions options,
        string serverAssemblyPath,
        IHostLifecycleEventSink events)
    {
        _supervisor = supervisor ?? throw new ArgumentNullException(nameof(supervisor));
        _options = options ?? throw new ArgumentNullException(nameof(options));
        ArgumentException.ThrowIfNullOrWhiteSpace(serverAssemblyPath);
        _serverAssemblyPath = Path.GetFullPath(serverAssemblyPath);
        _events = events ?? throw new ArgumentNullException(nameof(events));
    }

    public async Task<ManagedChildProcess> StartGatewayAsync(CancellationToken cancellationToken = default)
    {
        var plan = GatewayProcessPlan.Create(
            _serverAssemblyPath,
            _options.Paths,
            _options.LocalGatewayUri);

        var child = await _supervisor.StartAsync(plan, cancellationToken);
        await _events.PublishAsync(
            new HostLifecycleEvent(
                HostLifecycleEventKind.GatewayStarted,
                DateTimeOffset.UtcNow,
                "Loopback CRM gateway process started and awaits readiness proof.",
                child.ProcessId,
                GatewayProcessPlan.ComponentName),
            cancellationToken);

        return child;
    }

    public Task<IReadOnlyDictionary<string, int>> SnapshotAsync(CancellationToken cancellationToken = default) =>
        _supervisor.SnapshotAsync(cancellationToken);

    public async Task StopAsync(CancellationToken cancellationToken = default)
    {
        await _supervisor.StopAllAsync(_options.GracefulShutdownTimeout, cancellationToken);
    }

    public async ValueTask DisposeAsync()
    {
        await _supervisor.DisposeAsync();
    }
}
