using System.Diagnostics;
using System.Net;
using System.Net.Http.Json;
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
var repositoryRoot = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", ".."));
var serverDll = Path.Combine(repositoryRoot, "src", "InterLan.Server", "bin", "Release", "net10.0", "InterLan.Server.dll");

if (!File.Exists(serverDll))
{
    Console.Error.WriteLine($"Server DLL not found: {serverDll}");
    return 1;
}

using var process = new Process
{
    StartInfo = new ProcessStartInfo
    {
        FileName = "dotnet",
        Arguments = $""{serverDll}"",
        WorkingDirectory = repositoryRoot,
        UseShellExecute = false,
        RedirectStandardOutput = true,
        RedirectStandardError = true
    }
};

process.StartInfo.Environment["INTERLAN_DATA_DIR"] = root;
process.StartInfo.Environment["InterLan__Server__Port"] = port.ToString();
process.StartInfo.Environment["InterLan__Server__DiscoveryEnabled"] = "false";

process.Start();

using var handler = new HttpClientHandler
{
    ServerCertificateCustomValidationCallback = (_, _, _, _) => true
};
using var client = new HttpClient(handler)
{
    BaseAddress = new Uri($"https://127.0.0.1:{port}"),
    Timeout = TimeSpan.FromSeconds(3)
};

try
{
    var ready = false;
    for (var attempt = 0; attempt < 40; attempt++)
    {
        if (process.HasExited)
            break;

        try
        {
            using var health = await client.GetAsync("/health");
            if (health.IsSuccessStatusCode)
            {
                ready = true;
                break;
            }
        }
        catch
        {
            // Server is still starting.
        }

        await Task.Delay(250);
    }

    if (!ready)
    {
        Console.Error.WriteLine("FAIL HTTPS server did not become healthy.");
        Console.Error.WriteLine(await process.StandardError.ReadToEndAsync());
        return 1;
    }

    Console.WriteLine("PASS HTTPS Kestrel health endpoint");

    var before = await client.GetFromJsonAsync<JsonElement>("/api/v1/server/info");
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

    using var bootstrap = await client.PostAsJsonAsync("/api/v1/bootstrap/server", new
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

    using var duplicate = await client.PostAsJsonAsync("/api/v1/bootstrap/server", new
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

    var after = await client.GetFromJsonAsync<JsonElement>("/api/v1/server/info");
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
