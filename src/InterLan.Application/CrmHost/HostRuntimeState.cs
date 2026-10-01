namespace InterLan.Application.CrmHost;

public enum HostRuntimePhase
{
    Stopped = 0,
    Starting = 1,
    Degraded = 2,
    Ready = 3,
    Suspending = 4,
    Suspended = 5,
    Resuming = 6,
    Stopping = 7,
    Faulted = 8
}

public enum RuntimeComponentState
{
    Unknown = 0,
    Stopped = 1,
    Starting = 2,
    Ready = 3,
    Degraded = 4,
    Failed = 5
}
