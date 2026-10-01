namespace InterLan.Application.CrmHost;

public enum RuntimeRecoveryDisposition
{
    Clean = 0,
    ActiveOwner = 1,
    StaleState = 2
}

public sealed record RuntimeRecoveryAssessment(
    RuntimeRecoveryDisposition Disposition,
    RuntimeStateEnvelope? PersistedState,
    string Detail);

public sealed class RuntimeRecoveryInspector
{
    private readonly string _lockPath;
    private readonly RuntimeStateStore _stateStore;

    public RuntimeRecoveryInspector(string lockPath, RuntimeStateStore stateStore)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(lockPath);
        _lockPath = Path.GetFullPath(lockPath);
        _stateStore = stateStore ?? throw new ArgumentNullException(nameof(stateStore));
    }

    public async Task<RuntimeRecoveryAssessment> InspectAsync(CancellationToken cancellationToken = default)
    {
        var persisted = await _stateStore.ReadAsync(cancellationToken);

        if (IsLeaseOwned())
        {
            return new RuntimeRecoveryAssessment(
                RuntimeRecoveryDisposition.ActiveOwner,
                persisted,
                "The single-instance lease is currently owned; persisted PIDs are not used to override it.");
        }

        if (persisted is null)
        {
            return new RuntimeRecoveryAssessment(
                RuntimeRecoveryDisposition.Clean,
                null,
                "No active lease or persisted runtime state exists.");
        }

        return new RuntimeRecoveryAssessment(
            RuntimeRecoveryDisposition.StaleState,
            persisted,
            "Persisted runtime metadata has no live lease owner and may be safely replaced on next start.");
    }

    private bool IsLeaseOwned()
    {
        if (!File.Exists(_lockPath))
        {
            return false;
        }

        try
        {
            using var stream = new FileStream(
                _lockPath,
                FileMode.Open,
                FileAccess.ReadWrite,
                FileShare.None);
            return false;
        }
        catch (IOException)
        {
            return true;
        }
        catch (UnauthorizedAccessException)
        {
            return true;
        }
    }
}
