using InterLan.Application.CrmHost;

var tempRoot = Path.Combine(Path.GetTempPath(), $"interlan-crm-host-checks-{Guid.NewGuid():N}");
Directory.CreateDirectory(tempRoot);

try
{
    CheckLinuxPaths();
    CheckRuntimeOptions();
    CheckQuickTunnelParsing();
    CheckTunnelRestartPolicy();
    CheckCommandGrammar();
    CheckLifecyclePolicy();
    CheckBoundedOutput();
    CheckSingleInstanceLease();
    await CheckRuntimeStateStoreAsync();

    Console.WriteLine("InterLan CRM host checks PASS");
    return 0;
}
finally
{
    try
    {
        Directory.Delete(tempRoot, recursive: true);
    }
    catch (IOException)
    {
    }
}

void CheckLinuxPaths()
{
    var home = Path.Combine(tempRoot, "home");
    var paths = LinuxHostPaths.Resolve(
        new Dictionary<string, string?>(StringComparer.Ordinal)
        {
            ["HOME"] = home,
            ["XDG_CONFIG_HOME"] = Path.Combine(tempRoot, "config"),
            ["XDG_STATE_HOME"] = Path.Combine(tempRoot, "state"),
            ["XDG_DATA_HOME"] = Path.Combine(tempRoot, "data"),
            ["XDG_CACHE_HOME"] = Path.Combine(tempRoot, "cache"),
            ["XDG_RUNTIME_DIR"] = Path.Combine(tempRoot, "runtime")
        },
        home);

    Check(paths.DatabasePath.StartsWith(Path.Combine(tempRoot, "data"), StringComparison.Ordinal),
        "Database path must live under XDG_DATA_HOME.");
    Check(paths.LockFilePath.StartsWith(Path.Combine(tempRoot, "runtime"), StringComparison.Ordinal),
        "Runtime lease must live under XDG_RUNTIME_DIR.");
}

void CheckRuntimeOptions()
{
    var paths = LinuxHostPaths.Resolve(homeDirectory: Path.Combine(tempRoot, "options-home"));
    var valid = new HostRuntimeOptions { Paths = paths };
    valid.Validate();

    Expect<InvalidOperationException>(() =>
        (valid with { LocalGatewayUri = new Uri("http://0.0.0.0:5080") }).Validate(),
        "Non-loopback gateway binding must fail closed.");

    Expect<InvalidOperationException>(() =>
        (valid with { LocalBackendUri = new Uri("http://127.0.0.1:5443") }).Validate(),
        "Canonical backend must require loopback HTTPS.");
}

void CheckQuickTunnelParsing()
{
    Check(
        QuickTunnelUrlParser.TryParse(
            "INF Your quick Tunnel has been created! Visit it at https://alpha-bravo.trycloudflare.com",
            out var publicUri) &&
        publicUri?.Host == "alpha-bravo.trycloudflare.com",
        "Valid Quick Tunnel URL was not parsed.");

    Check(
        !QuickTunnelUrlParser.TryParse(
            "https://alpha.trycloudflare.com.evil.example",
            out _),
        "Host-suffix injection must be rejected.");

    Check(
        !QuickTunnelUrlParser.TryParse(
            "https://alpha.trycloudflare.com/?token=secret",
            out _),
        "Query-bearing tunnel URLs must be rejected.");

    Check(
        !QuickTunnelUrlParser.TryParse(
            "https://user@alpha.trycloudflare.com",
            out _),
        "User-info tunnel URLs must be rejected.");
}

void CheckTunnelRestartPolicy()
{
    var policy = new TunnelRestartPolicy(2, TimeSpan.FromMilliseconds(10));
    Check(!policy.Decide(0, networkAvailable: false).ShouldRestart,
        "Tunnel must not spin while the network is unavailable.");
    Check(policy.Decide(0, networkAvailable: true).ShouldRestart,
        "First connected-network restart should be allowed.");
    Check(!policy.Decide(2, networkAvailable: true).ShouldRestart,
        "Restart budget must be enforced.");
}

void CheckCommandGrammar()
{
    var start = InterLanHostCommand.Parse(["start", "--remote"]);
    Check(start.Kind == InterLanHostCommandKind.Start && start.RemoteAccess,
        "start --remote must request remote access.");

    var status = InterLanHostCommand.Parse(["status", "--json"]);
    Check(status.Kind == InterLanHostCommandKind.Status && status.JsonOutput,
        "status --json must request structured status.");

    Expect<ArgumentException>(() => InterLanHostCommand.Parse(["start", "--shell"]),
        "Unknown CLI options must fail closed.");
}

void CheckLifecyclePolicy()
{
    var state = new HostLifecycleStateMachine();
    state.TransitionTo(HostRuntimePhase.Starting);
    state.TransitionTo(HostRuntimePhase.Ready);
    Expect<InvalidOperationException>(() => state.TransitionTo(HostRuntimePhase.Stopped),
        "Ready host must not bypass orderly stopping.");
}

void CheckBoundedOutput()
{
    var output = new BoundedProcessOutput(maxLines: 2, maxLineLength: 4);
    output.Append("first");
    output.Append("second");
    output.Append("third");
    var snapshot = output.Snapshot();
    Check(snapshot.Count == 2, "Process output line count must be bounded.");
    Check(snapshot.All(line => line.Length <= 4), "Process output line length must be bounded.");
}

void CheckSingleInstanceLease()
{
    var lockPath = Path.Combine(tempRoot, "lease", "host.lock");
    using var first = SingleInstanceLease.Acquire(lockPath);
    Expect<HostAlreadyRunningException>(() =>
    {
        using var duplicate = SingleInstanceLease.Acquire(lockPath);
    }, "Concurrent host lease acquisition must fail.");

    first.Dispose();
    using var recovered = SingleInstanceLease.Acquire(lockPath);
    Check(File.Exists(lockPath), "Released stale pathname must be recoverable by a new lease.");
}

async Task CheckRuntimeStateStoreAsync()
{
    var statePath = Path.Combine(tempRoot, "state-store", "runtime-state.json");
    var store = new RuntimeStateStore(statePath);
    var snapshot = HostRuntimeSnapshot.Stopped(DateTimeOffset.UtcNow);
    await store.WriteAsync(snapshot, new Dictionary<string, int> { ["crm-backend"] = 42 });
    var loaded = await store.ReadAsync();
    Check(loaded is not null && loaded.SchemaVersion == 1,
        "Runtime state must round-trip with the supported schema.");
    Check(loaded!.ProcessIds["crm-backend"] == 42,
        "Runtime state must preserve diagnostic process metadata.");
}

static void Check(bool condition, string message)
{
    if (!condition)
    {
        throw new InvalidOperationException(message);
    }
}

static void Expect<TException>(Action action, string message)
    where TException : Exception
{
    try
    {
        action();
    }
    catch (TException)
    {
        return;
    }

    throw new InvalidOperationException(message);
}
