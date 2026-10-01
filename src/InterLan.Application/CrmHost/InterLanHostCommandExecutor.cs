namespace InterLan.Application.CrmHost;

public interface IHostRuntimeOperations
{
    HostRuntimeSnapshot Snapshot { get; }

    Task<HostRuntimeSnapshot> StartAsync(bool remoteAccess, CancellationToken cancellationToken = default);

    Task<HostRuntimeSnapshot> StopAsync(CancellationToken cancellationToken = default);
}

public sealed record InterLanHostCommandResult(
    int ExitCode,
    HostRuntimeSnapshot Snapshot,
    string Message);

public sealed class InterLanHostCommandExecutor
{
    private readonly IHostRuntimeOperations _runtime;
    private readonly SemaphoreSlim _mutations = new(1, 1);

    public InterLanHostCommandExecutor(IHostRuntimeOperations runtime)
    {
        _runtime = runtime ?? throw new ArgumentNullException(nameof(runtime));
    }

    public async Task<InterLanHostCommandResult> ExecuteAsync(
        InterLanHostCommand command,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(command);

        if (command.Kind == InterLanHostCommandKind.Status)
        {
            return new InterLanHostCommandResult(0, _runtime.Snapshot, "Runtime status read successfully.");
        }

        await _mutations.WaitAsync(cancellationToken);
        try
        {
            return command.Kind switch
            {
                InterLanHostCommandKind.Start => await StartAsync(command.RemoteAccess, cancellationToken),
                InterLanHostCommandKind.Stop => await StopAsync(cancellationToken),
                _ => throw new InvalidOperationException($"Unsupported host command: {command.Kind}.")
            };
        }
        finally
        {
            _mutations.Release();
        }
    }

    private async Task<InterLanHostCommandResult> StartAsync(bool remoteAccess, CancellationToken cancellationToken)
    {
        var current = _runtime.Snapshot;
        if (current.Phase is HostRuntimePhase.Ready or HostRuntimePhase.Degraded)
        {
            return new InterLanHostCommandResult(0, current, "INTER-LAN is already running.");
        }

        try
        {
            var snapshot = await _runtime.StartAsync(remoteAccess, cancellationToken);
            return new InterLanHostCommandResult(
                snapshot.IsLocallyOperational ? 0 : 1,
                snapshot,
                snapshot.IsRemoteAccessOperational || !remoteAccess
                    ? "INTER-LAN started."
                    : "INTER-LAN started locally with degraded remote access.");
        }
        catch (HostAlreadyRunningException ex)
        {
            return new InterLanHostCommandResult(2, _runtime.Snapshot, ex.Message);
        }
    }

    private async Task<InterLanHostCommandResult> StopAsync(CancellationToken cancellationToken)
    {
        var current = _runtime.Snapshot;
        if (current.Phase == HostRuntimePhase.Stopped)
        {
            return new InterLanHostCommandResult(0, current, "INTER-LAN is already stopped.");
        }

        var snapshot = await _runtime.StopAsync(cancellationToken);
        return new InterLanHostCommandResult(0, snapshot, "INTER-LAN stopped.");
    }
}
