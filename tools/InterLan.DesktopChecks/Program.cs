using System.Net;
using System.Net.Sockets;
using InterLan.Desktop;
using InterLan.Testing;

var failures = new List<string>();

void Check(bool condition, string name)
{
    Console.WriteLine($"{(condition ? "PASS" : "FAIL")} {name}");
    if (!condition) failures.Add(name);
}

var root = Path.Combine(
    Path.GetTempPath(),
    "interlan-desktop-host-" + Guid.NewGuid().ToString("N"));
Directory.CreateDirectory(root);

try
{
    using var portLease = LoopbackPortLease.Reserve();
    var port = portLease.Port;
    portLease.Release();

    await using var runtime = new OwnerServerRuntime();

    await runtime.StartAsync(
        root,
        new Dictionary<string, string?>
        {
            ["InterLan:Server:BindAddress"] = "127.0.0.1",
            ["InterLan:Server:Port"] = port.ToString(),
            ["InterLan:Server:DiscoveryEnabled"] = "false"
        });

    Check(runtime.IsRunning, "desktop owner runtime starts embedded canonical server");

    using var client = TestHttpClientFactory.CreateLoopback();
    client.BaseAddress = new Uri($"https://127.0.0.1:{port}");

    using var health = await client.GetAsync("/health");
    Check(
        health.IsSuccessStatusCode,
        "desktop-owned server exposes HTTPS health endpoint");

    await runtime.StopAsync();

    Check(!runtime.IsRunning, "desktop owner runtime stops embedded server");

    var socketClosed = false;
    try
    {
        using var tcp = new TcpClient(AddressFamily.InterNetwork);
        await tcp.ConnectAsync(IPAddress.Loopback, port)
            .WaitAsync(TimeSpan.FromSeconds(1));
    }
    catch
    {
        socketClosed = true;
    }

    Check(
        socketClosed,
        "desktop owner runtime releases listener when stopped");
}
finally
{
    try { Directory.Delete(root, recursive: true); } catch { }
}

if (failures.Count > 0)
{
    Console.Error.WriteLine(
        $"Desktop checks failed: {string.Join(", ", failures)}");
    return 1;
}

Console.WriteLine("INTER-LAN DESKTOP OWNER-HOST CHECKS: PASS");
return 0;
