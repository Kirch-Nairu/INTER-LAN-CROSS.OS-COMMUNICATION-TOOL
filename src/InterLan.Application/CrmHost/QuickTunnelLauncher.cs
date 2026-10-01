namespace InterLan.Application.CrmHost;

public sealed record QuickTunnelHandle(
    int ProcessId,
    Uri PublicUri,
    DateTimeOffset ConnectedAt);

public sealed class QuickTunnelLauncher
{
    private readonly ProcessSupervisor _supervisor;

    public QuickTunnelLauncher(ProcessSupervisor supervisor)
    {
        _supervisor = supervisor ?? throw new ArgumentNullException(nameof(supervisor));
    }

    public async Task<QuickTunnelHandle> StartAsync(
        string executablePath,
        Uri localGatewayUri,
        TimeSpan startupTimeout,
        CancellationToken cancellationToken = default)
    {
        if (startupTimeout <= TimeSpan.Zero)
        {
            throw new ArgumentOutOfRangeException(nameof(startupTimeout));
        }

        var plan = CloudflaredQuickTunnelPlan.Create(executablePath, localGatewayUri);
        var child = await _supervisor.StartAsync(plan, cancellationToken);

        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeout.CancelAfter(startupTimeout);

        try
        {
            while (true)
            {
                if (TryFindUrl(child.StandardOutputSnapshot(), out var publicUri) ||
                    TryFindUrl(child.StandardErrorSnapshot(), out publicUri))
                {
                    return new QuickTunnelHandle(child.ProcessId, publicUri!, DateTimeOffset.UtcNow);
                }

                if (child.HasExited)
                {
                    var exit = await child.WaitForExitAsync(cancellationToken);
                    throw new InvalidOperationException(
                        $"cloudflared exited with code {exit.ExitCode} before publishing a valid Quick Tunnel URL.");
                }

                await Task.Delay(TimeSpan.FromMilliseconds(100), timeout.Token);
            }
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            await _supervisor.StopAsync(
                CloudflaredQuickTunnelPlan.ComponentName,
                TimeSpan.FromSeconds(2),
                CancellationToken.None);
            throw new TimeoutException("cloudflared did not publish a valid Quick Tunnel URL before the startup timeout.");
        }
        catch
        {
            await _supervisor.StopAsync(
                CloudflaredQuickTunnelPlan.ComponentName,
                TimeSpan.FromSeconds(2),
                CancellationToken.None);
            throw;
        }
    }

    private static bool TryFindUrl(IReadOnlyList<string> lines, out Uri? publicUri)
    {
        for (var index = lines.Count - 1; index >= 0; index--)
        {
            if (QuickTunnelUrlParser.TryParse(lines[index], out publicUri))
            {
                return true;
            }
        }

        publicUri = null;
        return false;
    }
}
