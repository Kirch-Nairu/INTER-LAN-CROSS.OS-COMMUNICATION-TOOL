namespace InterLan.Application.CrmHost;

public enum HostLifecycleEventKind
{
    StartRequested,
    PrerequisitesValidated,
    LeaseAcquired,
    BackendStarted,
    GatewayStarted,
    NativeControlStarted,
    RemoteAccessStarted,
    RemoteAccessDegraded,
    Ready,
    SuspendRequested,
    Suspended,
    ResumeRequested,
    Resumed,
    StopRequested,
    ChildExited,
    Faulted,
    Stopped
}

public sealed record HostLifecycleEvent(
    HostLifecycleEventKind Kind,
    DateTimeOffset OccurredAt,
    string Message,
    int? ProcessId = null,
    string? Component = null);

public interface IHostLifecycleEventSink
{
    ValueTask PublishAsync(HostLifecycleEvent lifecycleEvent, CancellationToken cancellationToken = default);
}

public sealed class InMemoryHostLifecycleEventSink : IHostLifecycleEventSink
{
    private readonly object _gate = new();
    private readonly List<HostLifecycleEvent> _events = [];

    public IReadOnlyList<HostLifecycleEvent> Snapshot()
    {
        lock (_gate)
        {
            return _events.ToArray();
        }
    }

    public ValueTask PublishAsync(HostLifecycleEvent lifecycleEvent, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        ArgumentNullException.ThrowIfNull(lifecycleEvent);

        lock (_gate)
        {
            _events.Add(lifecycleEvent);
        }

        return ValueTask.CompletedTask;
    }
}
