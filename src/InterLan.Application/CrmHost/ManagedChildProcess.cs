using System.Diagnostics;

namespace InterLan.Application.CrmHost;

public sealed record ManagedProcessExit(
    string Component,
    int ProcessId,
    int ExitCode,
    DateTimeOffset ExitedAt,
    IReadOnlyList<string> StandardOutput,
    IReadOnlyList<string> StandardError);

public sealed class ManagedChildProcess : IAsyncDisposable
{
    private readonly Process _process;
    private readonly BoundedProcessOutput _standardOutput = new();
    private readonly BoundedProcessOutput _standardError = new();
    private readonly TaskCompletionSource<ManagedProcessExit> _exit =
        new(TaskCreationOptions.RunContinuationsAsynchronously);
    private int _disposed;

    private ManagedChildProcess(string component, Process process, bool captureOutput)
    {
        Component = component;
        _process = process;
        _process.EnableRaisingEvents = true;
        _process.Exited += (_, _) => CompleteExit();

        if (captureOutput)
        {
            _process.OutputDataReceived += (_, args) => _standardOutput.Append(args.Data);
            _process.ErrorDataReceived += (_, args) => _standardError.Append(args.Data);
            _process.BeginOutputReadLine();
            _process.BeginErrorReadLine();
        }

        if (_process.HasExited)
        {
            CompleteExit();
        }
    }

    public string Component { get; }

    public int ProcessId => _process.Id;

    public bool HasExited
    {
        get
        {
            try
            {
                return _process.HasExited;
            }
            catch (InvalidOperationException)
            {
                return true;
            }
        }
    }

    public IReadOnlyList<string> StandardOutputSnapshot() => _standardOutput.Snapshot();

    public IReadOnlyList<string> StandardErrorSnapshot() => _standardError.Snapshot();

    public static ManagedChildProcess Start(ProcessLaunchSpec spec)
    {
        ArgumentNullException.ThrowIfNull(spec);

        var process = new Process { StartInfo = spec.ToStartInfo() };
        try
        {
            if (!process.Start())
            {
                throw new InvalidOperationException($"Failed to start '{spec.Component}'.");
            }

            return new ManagedChildProcess(spec.Component, process, spec.CaptureOutput);
        }
        catch
        {
            process.Dispose();
            throw;
        }
    }

    public async Task<ManagedProcessExit> WaitForExitAsync(CancellationToken cancellationToken = default) =>
        await _exit.Task.WaitAsync(cancellationToken);

    public async Task<ManagedProcessExit> StopAsync(TimeSpan timeout, CancellationToken cancellationToken = default)
    {
        if (timeout <= TimeSpan.Zero)
        {
            throw new ArgumentOutOfRangeException(nameof(timeout));
        }

        if (!HasExited)
        {
            using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            timeoutCts.CancelAfter(timeout);

            try
            {
                await _exit.Task.WaitAsync(timeoutCts.Token);
            }
            catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
            {
                if (!HasExited)
                {
                    _process.Kill(entireProcessTree: true);
                }
            }
        }

        return await _exit.Task.WaitAsync(cancellationToken);
    }

    public async ValueTask DisposeAsync()
    {
        if (Interlocked.Exchange(ref _disposed, 1) != 0)
        {
            return;
        }

        if (!HasExited)
        {
            try
            {
                _process.Kill(entireProcessTree: true);
            }
            catch (InvalidOperationException)
            {
            }
        }

        try
        {
            await _exit.Task.WaitAsync(TimeSpan.FromSeconds(5));
        }
        catch (TimeoutException)
        {
        }

        _process.Dispose();
    }

    private void CompleteExit()
    {
        try
        {
            _exit.TrySetResult(new ManagedProcessExit(
                Component,
                _process.Id,
                _process.ExitCode,
                DateTimeOffset.UtcNow,
                _standardOutput.Snapshot(),
                _standardError.Snapshot()));
        }
        catch (InvalidOperationException)
        {
        }
    }
}
