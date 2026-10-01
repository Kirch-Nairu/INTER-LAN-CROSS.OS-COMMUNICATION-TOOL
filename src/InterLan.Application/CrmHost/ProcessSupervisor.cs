namespace InterLan.Application.CrmHost;

public sealed class ProcessSupervisor : IAsyncDisposable
{
    private readonly SemaphoreSlim _gate = new(1, 1);
    private readonly Dictionary<string, ManagedChildProcess> _children = new(StringComparer.Ordinal);
    private bool _disposed;

    public async Task<ManagedChildProcess> StartAsync(
        ProcessLaunchSpec spec,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(spec);

        await _gate.WaitAsync(cancellationToken);
        try
        {
            ThrowIfDisposed();

            if (_children.TryGetValue(spec.Component, out var existing) && !existing.HasExited)
            {
                throw new InvalidOperationException($"Component '{spec.Component}' is already running.");
            }

            if (existing is not null)
            {
                _children.Remove(spec.Component);
                await existing.DisposeAsync();
            }

            var child = ManagedChildProcess.Start(spec);
            _children.Add(spec.Component, child);
            _ = ObserveExitAsync(spec.Component, child);
            return child;
        }
        finally
        {
            _gate.Release();
        }
    }

    public async Task<ManagedProcessExit?> StopAsync(
        string component,
        TimeSpan timeout,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(component);

        ManagedChildProcess? child;
        await _gate.WaitAsync(cancellationToken);
        try
        {
            if (!_children.TryGetValue(component, out child))
            {
                return null;
            }
        }
        finally
        {
            _gate.Release();
        }

        var exit = await child.StopAsync(timeout, cancellationToken);

        await _gate.WaitAsync(cancellationToken);
        try
        {
            if (_children.TryGetValue(component, out var current) && ReferenceEquals(current, child))
            {
                _children.Remove(component);
            }
        }
        finally
        {
            _gate.Release();
        }

        await child.DisposeAsync();
        return exit;
    }

    public async Task<IReadOnlyDictionary<string, int>> SnapshotAsync(CancellationToken cancellationToken = default)
    {
        await _gate.WaitAsync(cancellationToken);
        try
        {
            return _children
                .Where(pair => !pair.Value.HasExited)
                .ToDictionary(pair => pair.Key, pair => pair.Value.ProcessId, StringComparer.Ordinal);
        }
        finally
        {
            _gate.Release();
        }
    }

    public async Task StopAllAsync(TimeSpan timeout, CancellationToken cancellationToken = default)
    {
        string[] components;
        await _gate.WaitAsync(cancellationToken);
        try
        {
            components = _children.Keys.Reverse().ToArray();
        }
        finally
        {
            _gate.Release();
        }

        foreach (var component in components)
        {
            await StopAsync(component, timeout, cancellationToken);
        }
    }

    public async ValueTask DisposeAsync()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        try
        {
            await StopAllAsync(TimeSpan.FromSeconds(5));
        }
        finally
        {
            _gate.Dispose();
        }
    }

    private async Task ObserveExitAsync(string component, ManagedChildProcess child)
    {
        try
        {
            await child.WaitForExitAsync();
            await _gate.WaitAsync();
            try
            {
                if (_children.TryGetValue(component, out var current) && ReferenceEquals(current, child))
                {
                    _children.Remove(component);
                }
            }
            finally
            {
                _gate.Release();
            }
        }
        catch (ObjectDisposedException)
        {
        }
    }

    private void ThrowIfDisposed()
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
    }
}
