namespace InterLan.Server;

public sealed class InterLanServerInstance : IAsyncDisposable
{
    private readonly WebApplication _application;
    private int _started;

    internal InterLanServerInstance(WebApplication application)
    {
        _application = application;
    }

    public IServiceProvider Services => _application.Services;

    public async Task StartAsync(CancellationToken cancellationToken = default)
    {
        if (Interlocked.Exchange(ref _started, 1) != 0)
            throw new InvalidOperationException("INTER-LAN server instance is already started.");

        try
        {
            await _application.StartAsync(cancellationToken);
        }
        catch
        {
            Interlocked.Exchange(ref _started, 0);
            throw;
        }
    }

    public async Task StopAsync(CancellationToken cancellationToken = default)
    {
        if (Interlocked.Exchange(ref _started, 0) == 0)
            return;

        await _application.StopAsync(cancellationToken);
    }

    public async ValueTask DisposeAsync()
    {
        await StopAsync();
        await _application.DisposeAsync();
    }
}
