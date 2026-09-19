using System.Collections.Concurrent;
using System.Diagnostics;

namespace InterLan.Testing;

public sealed class InterLanServerProcess : IAsyncDisposable
{
    private readonly Process _process;
    private readonly bool _deleteDataDirectory;
    private readonly ConcurrentQueue<string> _output;
    private readonly ConcurrentQueue<string> _errors;

    private InterLanServerProcess(
        Process process,
        Uri baseUri,
        string dataDirectory,
        bool deleteDataDirectory,
        ConcurrentQueue<string> output,
        ConcurrentQueue<string> errors)
    {
        _process = process;
        BaseUri = baseUri;
        DataDirectory = dataDirectory;
        _deleteDataDirectory = deleteDataDirectory;
        _output = output;
        _errors = errors;
    }

    public Uri BaseUri { get; }
    public string DataDirectory { get; }
    public bool HasExited => _process.HasExited;
    public int? ExitCode => _process.HasExited ? _process.ExitCode : null;

    public IReadOnlyList<string> RecentOutput(int count = 40) =>
        _output.TakeLast(count).ToArray();

    public IReadOnlyList<string> RecentErrors(int count = 40) =>
        _errors.TakeLast(count).ToArray();

    public HttpClient CreateClient()
    {
        var client = TestHttpClientFactory.CreateLoopback();
        client.BaseAddress = BaseUri;
        return client;
    }

    public static async Task<InterLanServerProcess> StartAsync(
        string? repositoryRoot = null,
        string? dataDirectory = null,
        IReadOnlyDictionary<string, string?>? environmentOverrides = null,
        int maxAttempts = 3,
        CancellationToken cancellationToken = default)
    {
        if (maxAttempts < 1)
            throw new ArgumentOutOfRangeException(nameof(maxAttempts));

        repositoryRoot = RepositoryLayout.FindRoot(repositoryRoot);
        var serverDll = RepositoryLayout.ServerDll(repositoryRoot);
        if (!File.Exists(serverDll))
            throw new FileNotFoundException("INTER-LAN server DLL was not built.", serverDll);

        var ownsDataDirectory = string.IsNullOrWhiteSpace(dataDirectory);
        dataDirectory = ownsDataDirectory
            ? Path.Combine(Path.GetTempPath(), "interlan-server-" + Guid.NewGuid().ToString("N"))
            : Path.GetFullPath(dataDirectory!);
        Directory.CreateDirectory(dataDirectory);

        Exception? lastFailure = null;
        var aggregateOutput = new ConcurrentQueue<string>();
        var aggregateErrors = new ConcurrentQueue<string>();

        for (var attempt = 1; attempt <= maxAttempts; attempt++)
        {
            cancellationToken.ThrowIfCancellationRequested();

            using var portLease = LoopbackPortLease.Reserve();
            var port = portLease.Port;
            var baseUri = new Uri($"https://127.0.0.1:{port}");

            var startInfo = new ProcessStartInfo
            {
                FileName = "dotnet",
                WorkingDirectory = repositoryRoot,
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true
            };
            startInfo.ArgumentList.Add(serverDll);
            startInfo.Environment["INTERLAN_DATA_DIR"] = dataDirectory;
            startInfo.Environment["InterLan__Server__Port"] = port.ToString();
            startInfo.Environment["InterLan__Server__BindAddress"] = "127.0.0.1";
            startInfo.Environment["InterLan__Server__DiscoveryEnabled"] = "false";
            startInfo.Environment["ASPNETCORE_CONTENTROOT"] =
                Path.Combine(repositoryRoot, "src", "InterLan.Server");

            if (environmentOverrides is not null)
            {
                foreach (var pair in environmentOverrides)
                    startInfo.Environment[pair.Key] = pair.Value;
            }

            var output = new ConcurrentQueue<string>();
            var errors = new ConcurrentQueue<string>();
            var process = new Process { StartInfo = startInfo };

            process.OutputDataReceived += (_, e) =>
            {
                if (e.Data is null) return;
                output.Enqueue(e.Data);
                aggregateOutput.Enqueue($"attempt {attempt}: {e.Data}");
            };
            process.ErrorDataReceived += (_, e) =>
            {
                if (e.Data is null) return;
                errors.Enqueue(e.Data);
                aggregateErrors.Enqueue($"attempt {attempt}: {e.Data}");
            };

            try
            {
                // Hold the loopback port until immediately before child creation.
                portLease.Release();

                if (!process.Start())
                    throw new InvalidOperationException("dotnet child process could not be started.");

                process.BeginOutputReadLine();
                process.BeginErrorReadLine();

                if (await WaitUntilHealthyAsync(
                    process,
                    baseUri,
                    cancellationToken))
                {
                    return new InterLanServerProcess(
                        process,
                        baseUri,
                        dataDirectory,
                        ownsDataDirectory,
                        output,
                        errors);
                }

                lastFailure = new InvalidOperationException(
                    $"Server did not become healthy on attempt {attempt}.");
            }
            catch (Exception exception) when (!cancellationToken.IsCancellationRequested)
            {
                lastFailure = exception;
            }

            if (!process.HasExited)
            {
                process.Kill(entireProcessTree: true);
                await process.WaitForExitAsync(cancellationToken);
            }

            process.Dispose();

            if (attempt < maxAttempts)
                await Task.Delay(TimeSpan.FromMilliseconds(150), cancellationToken);
        }

        if (ownsDataDirectory)
        {
            try { Directory.Delete(dataDirectory, recursive: true); }
            catch { }
        }

        var diagnostics = string.Join(
            Environment.NewLine,
            aggregateOutput.TakeLast(40).Select(line => "OUT " + line)
                .Concat(aggregateErrors.TakeLast(40).Select(line => "ERR " + line)));

        throw new InvalidOperationException(
            $"INTER-LAN server failed to start after {maxAttempts} attempts.{Environment.NewLine}{diagnostics}",
            lastFailure);
    }

    private static async Task<bool> WaitUntilHealthyAsync(
        Process process,
        Uri baseUri,
        CancellationToken cancellationToken)
    {
        using var client = TestHttpClientFactory.CreateLoopback(
            TimeSpan.FromSeconds(3));

        for (var attempt = 0; attempt < 40; attempt++)
        {
            cancellationToken.ThrowIfCancellationRequested();

            if (process.HasExited)
                return false;

            try
            {
                using var response = await client.GetAsync(
                    new Uri(baseUri, "/health"),
                    cancellationToken);

                if (response.IsSuccessStatusCode)
                    return true;
            }
            catch (HttpRequestException)
            {
                // The server is still starting.
            }
            catch (TaskCanceledException) when (!cancellationToken.IsCancellationRequested)
            {
                // A single startup probe timed out.
            }

            await Task.Delay(TimeSpan.FromMilliseconds(250), cancellationToken);
        }

        return false;
    }

    public async ValueTask DisposeAsync()
    {
        try
        {
            if (!_process.HasExited)
            {
                _process.Kill(entireProcessTree: true);
                await _process.WaitForExitAsync();
            }
        }
        finally
        {
            _process.Dispose();

            if (_deleteDataDirectory)
            {
                try { Directory.Delete(DataDirectory, recursive: true); }
                catch { }
            }
        }
    }
}
