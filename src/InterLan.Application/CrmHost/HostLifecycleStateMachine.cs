namespace InterLan.Application.CrmHost;

public sealed class HostLifecycleStateMachine
{
    private readonly object _gate = new();
    private HostRuntimePhase _phase = HostRuntimePhase.Stopped;

    public HostRuntimePhase Phase
    {
        get
        {
            lock (_gate)
            {
                return _phase;
            }
        }
    }

    public HostRuntimePhase TransitionTo(HostRuntimePhase next)
    {
        lock (_gate)
        {
            if (_phase == next)
            {
                return _phase;
            }

            if (!IsAllowed(_phase, next))
            {
                throw new InvalidOperationException($"Illegal CRM host transition: {_phase} -> {next}.");
            }

            _phase = next;
            return _phase;
        }
    }

    public bool TryTransition(HostRuntimePhase expected, HostRuntimePhase next)
    {
        lock (_gate)
        {
            if (_phase != expected || !IsAllowed(_phase, next))
            {
                return false;
            }

            _phase = next;
            return true;
        }
    }

    private static bool IsAllowed(HostRuntimePhase current, HostRuntimePhase next) =>
        (current, next) switch
        {
            (HostRuntimePhase.Stopped, HostRuntimePhase.Starting) => true,
            (HostRuntimePhase.Starting, HostRuntimePhase.Ready) => true,
            (HostRuntimePhase.Starting, HostRuntimePhase.Degraded) => true,
            (HostRuntimePhase.Starting, HostRuntimePhase.Faulted) => true,
            (HostRuntimePhase.Ready, HostRuntimePhase.Degraded) => true,
            (HostRuntimePhase.Ready, HostRuntimePhase.Suspending) => true,
            (HostRuntimePhase.Ready, HostRuntimePhase.Stopping) => true,
            (HostRuntimePhase.Degraded, HostRuntimePhase.Ready) => true,
            (HostRuntimePhase.Degraded, HostRuntimePhase.Suspending) => true,
            (HostRuntimePhase.Degraded, HostRuntimePhase.Stopping) => true,
            (HostRuntimePhase.Suspending, HostRuntimePhase.Suspended) => true,
            (HostRuntimePhase.Suspended, HostRuntimePhase.Resuming) => true,
            (HostRuntimePhase.Resuming, HostRuntimePhase.Ready) => true,
            (HostRuntimePhase.Resuming, HostRuntimePhase.Degraded) => true,
            (HostRuntimePhase.Resuming, HostRuntimePhase.Faulted) => true,
            (HostRuntimePhase.Faulted, HostRuntimePhase.Starting) => true,
            (HostRuntimePhase.Faulted, HostRuntimePhase.Stopping) => true,
            (HostRuntimePhase.Stopping, HostRuntimePhase.Stopped) => true,
            _ => false
        };
}
