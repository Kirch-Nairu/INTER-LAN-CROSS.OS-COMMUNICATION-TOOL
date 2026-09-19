using InterLan.Testing;
using System.Collections.Concurrent;
using System.Diagnostics;
using System.Net;
using System.Net.Http.Json;
using System.Net.Security;
using System.Net.Sockets;
using System.Text.Json;

var root = Path.Combine(Path.GetTempPath(), "interlan-network-" + Guid.NewGuid().ToString("N"));
Directory.CreateDirectory(root);

static int ReservePort()
{
    using var listener = new TcpListener(IPAddress.Loopback, 0);
    listener.Start();
    return ((IPEndPoint)listener.LocalEndpoint).Port;
}

var port = ReservePort();
var repositoryRoot = RepositoryLayout.FindRoot();
var serverDll = RepositoryLayout.ServerDll(repositoryRoot);

if (!File.Exists(serverDll))
{
    Console.Error.WriteLine($"Server DLL not found: {serverDll}");
    return 1;
}

var startInfo = new ProcessStartInfo
{
    FileName = "dotnet",
    WorkingDirectory = repositoryRoot,
    UseShellExecute = false,
    RedirectStandardOutput = true,
    RedirectStandardError = true
};
startInfo.ArgumentList.Add(serverDll);
startInfo.Environment["INTERLAN_DATA_DIR"] = root;
startInfo.Environment["InterLan__Server__Port"] = port.ToString();
startInfo.Environment["InterLan__Server__BindAddress"] = "127.0.0.1";
startInfo.Environment["InterLan__Server__DiscoveryEnabled"] = "false";
startInfo.Environment["ASPNETCORE_CONTENTROOT"] = Path.Combine(repositoryRoot, "src", "InterLan.Server");

var output = new ConcurrentQueue<string>();
var errors = new ConcurrentQueue<string>();

using var process = new Process
{
    StartInfo = startInfo,
    EnableRaisingEvents = true
};
process.OutputDataReceived += (_, eventArgs) =>
{
    if (eventArgs.Data is not null) output.Enqueue(eventArgs.Data);
};
process.ErrorDataReceived += (_, eventArgs) =>
{
    if (eventArgs.Data is not null) errors.Enqueue(eventArgs.Data);
};

process.Start();
process.BeginOutputReadLine();
process.BeginErrorReadLine();

using var handler = new SocketsHttpHandler
{
    UseProxy = false,
    ConnectTimeout = TimeSpan.FromSeconds(2),
    SslOptions = new SslClientAuthenticationOptions
    {
        RemoteCertificateValidationCallback = (_, _, _, _) => true
    }
};
using var client = new HttpClient(handler)
{
    Timeout = TimeSpan.FromSeconds(3)
};

var probeBases = new[]
{
    new Uri($"https://127.0.0.1:{port}"),
    new Uri($"https://localhost:{port}")
};
Uri? activeBase = null;

try
{
    var tcpReady = false;
    string? tcpFailure = null;

    for (var attempt = 0; attempt < 40; attempt++)
    {
        if (process.HasExited)
            break;

        try
        {
            using var tcp = new TcpClient(AddressFamily.InterNetwork);
            await tcp.ConnectAsync(IPAddress.Loopback, port).WaitAsync(TimeSpan.FromSeconds(1));
            tcpReady = true;
            break;
        }
        catch (Exception exception)
        {
            tcpFailure = $"{exception.GetType().Name}: {exception.Message}";
            await Task.Delay(250);
        }
    }

    if (!tcpReady)
    {
        Console.Error.WriteLine($"FAIL TCP loopback did not become reachable on 127.0.0.1:{port}. LastError={tcpFailure}");
        foreach (var line in output.TakeLast(40)) Console.Error.WriteLine($"SERVER OUT: {line}");
        foreach (var line in errors.TakeLast(40)) Console.Error.WriteLine($"SERVER ERR: {line}");
        return 1;
    }

    Console.WriteLine($"PASS TCP loopback 127.0.0.1:{port}");

    var ready = false;
    var probeFailures = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

    for (var attempt = 0; attempt < 20; attempt++)
    {
        if (process.HasExited)
            break;

        foreach (var candidateBase in probeBases)
        {
            try
            {
                using var health = await client.GetAsync(new Uri(candidateBase, "/health"));
                if (health.IsSuccessStatusCode)
                {
                    activeBase = candidateBase;
                    ready = true;
                    break;
                }

                probeFailures[candidateBase.ToString()] = $"HTTP {(int)health.StatusCode}";
            }
            catch (Exception exception)
            {
                probeFailures[candidateBase.ToString()] =
                    $"{exception.GetType().Name}: {exception.GetBaseException().Message}";
            }
        }

        if (ready)
            break;

        await Task.Delay(250);
    }

    if (!ready)
    {
        Console.Error.WriteLine($"FAIL HTTPS server did not become healthy on port {port}. ProcessExited={process.HasExited} ExitCode={(process.HasExited ? process.ExitCode : -1)}");
        foreach (var failure in probeFailures)
            Console.Error.WriteLine($"PROBE {failure.Key}: {failure.Value}");
        foreach (var line in output.TakeLast(40)) Console.Error.WriteLine($"SERVER OUT: {line}");
        foreach (var line in errors.TakeLast(40)) Console.Error.WriteLine($"SERVER ERR: {line}");
        return 1;
    }

    Console.WriteLine($"PASS HTTPS Kestrel health endpoint via {activeBase}");
    Uri At(string path) => new(activeBase!, path);

    var before = await client.GetFromJsonAsync<JsonElement>(At("/api/v1/server/info"));
    var beforeServer = before.GetProperty("server");
    if (beforeServer.GetProperty("configured").GetBoolean())
    {
        Console.Error.WriteLine("FAIL fresh server unexpectedly configured");
        return 1;
    }

    var fingerprint = before.GetProperty("certificateSha256").GetString();
    if (fingerprint is null || fingerprint.Length != 64 || !fingerprint.All(Uri.IsHexDigit))
    {
        Console.Error.WriteLine("FAIL server info missing TLS fingerprint");
        return 1;
    }

    Console.WriteLine("PASS server info publishes pairing fingerprint");

    using var bootstrap = await client.PostAsJsonAsync(At("/api/v1/bootstrap/server"), new
    {
        serverName = "Network Smoke",
        ownerUsername = "owner",
        ownerDisplayName = "Owner",
        ownerPassword = "network-smoke-password"
    });

    if (!bootstrap.IsSuccessStatusCode)
    {
        Console.Error.WriteLine($"FAIL bootstrap HTTP {(int)bootstrap.StatusCode}: {await bootstrap.Content.ReadAsStringAsync()}");
        return 1;
    }

    Console.WriteLine("PASS loopback bootstrap endpoint");

    using var duplicate = await client.PostAsJsonAsync(At("/api/v1/bootstrap/server"), new
    {
        serverName = "Second",
        ownerUsername = "owner2",
        ownerDisplayName = "Owner Two",
        ownerPassword = "network-smoke-password-2"
    });

    if (duplicate.IsSuccessStatusCode)
    {
        Console.Error.WriteLine("FAIL duplicate bootstrap was accepted");
        return 1;
    }

    Console.WriteLine("PASS duplicate bootstrap fails closed");

    var after = await client.GetFromJsonAsync<JsonElement>(At("/api/v1/server/info"));
    if (!after.GetProperty("server").GetProperty("configured").GetBoolean())
    {
        Console.Error.WriteLine("FAIL configured server state not visible after bootstrap");
        return 1;
    }

    Console.WriteLine("PASS configured server identity survives through real HTTPS API");
    Console.WriteLine("INTER-LAN P1 NETWORK SMOKE: PASS");
    return 0;
}
finally
{
    if (!process.HasExited)
    {
        process.Kill(entireProcessTree: true);
        await process.WaitForExitAsync();
    }

    try
    {
        Directory.Delete(root, recursive: true);
    }
    catch
    {
        // Best effort.
    }
}
