namespace InterLan.Application.CrmHost;

public sealed class LoopbackReadinessProbe
{
    private readonly HttpMessageHandler? _handler;

    public LoopbackReadinessProbe(HttpMessageHandler? handler = null)
    {
        _handler = handler;
    }

    public async Task WaitUntilReadyAsync(
        Uri baseUri,
        string healthPath,
        TimeSpan timeout,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(baseUri);
        ArgumentException.ThrowIfNullOrWhiteSpace(healthPath);

        if (!baseUri.IsLoopback)
        {
            throw new InvalidOperationException("Runtime readiness probes may only target loopback services.");
        }

        if (timeout <= TimeSpan.Zero)
        {
            throw new ArgumentOutOfRangeException(nameof(timeout));
        }

        using var client = _handler is null
            ? new HttpClient()
            : new HttpClient(_handler, disposeHandler: false);
        client.BaseAddress = baseUri;

        using var deadline = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        deadline.CancelAfter(timeout);

        Exception? lastFailure = null;
        while (!deadline.IsCancellationRequested)
        {
            try
            {
                using var response = await client.GetAsync(healthPath, deadline.Token);
                if (response.IsSuccessStatusCode)
                {
                    return;
                }

                lastFailure = new HttpRequestException(
                    $"Readiness endpoint returned HTTP {(int)response.StatusCode}.");
            }
            catch (Exception ex) when (
                (ex is HttpRequestException or TaskCanceledException) &&
                !cancellationToken.IsCancellationRequested)
            {
                lastFailure = ex;
            }

            try
            {
                await Task.Delay(TimeSpan.FromMilliseconds(150), deadline.Token);
            }
            catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
            {
                break;
            }
        }

        cancellationToken.ThrowIfCancellationRequested();
        throw new TimeoutException(
            $"Loopback runtime did not become ready within {timeout}.",
            lastFailure);
    }
}
