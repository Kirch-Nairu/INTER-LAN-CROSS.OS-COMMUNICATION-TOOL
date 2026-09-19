using InterLan.Server;

namespace InterLan.Desktop;

public sealed class OwnerServerRuntime : IAsyncDisposable
{
    private readonly SemaphoreSlim _gate = new(1, 1);
    private InterLanServerInstance? _instance;

    public bool IsRunning => _instance is not null;

    public async Task StartAsync(
        string dataDirectory,
        IReadOnlyDictionary<string, string?>? configurationOverrides = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(dataDirectory);

        await _gate.WaitAsync(cancellationToken);
        try
        {
            if (_instance is not null)
                throw new InvalidOperationException("Owner server is already running.");

            var options = new InterLanServerHostOptions(
                Path.GetFullPath(dataDirectory),
                configurationOverrides);

            var instance = await InterLanServerHost.CreateAsync(
                Array.Empty<string>(),
                options,
                cancellationToken);

            try
            {
                await instance.StartAsync(cancellationToken);
                _instance = instance;
            }
            catch
            {
                await instance.DisposeAsync();
                throw;
            }
        }
        finally
        {
            _gate.Release();
        }
    }

    public async Task StopAsync(CancellationToken cancellationToken = default)
    {
        await _gate.WaitAsync(cancellationToken);
        try
        {
            if (_instance is null)
                return;

            var instance = _instance;
            _instance = null;
            await instance.DisposeAsync();
        }
        finally
        {
            _gate.Release();
        }
    }

    public async ValueTask DisposeAsync()
    {
        await StopAsync();
        _gate.Dispose();
    }
}
