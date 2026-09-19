using System.Diagnostics;
using InterLan.Testing;

var repositoryRoot = RepositoryLayout.FindRoot();

var checks = new[]
{
    ("P0 FOUNDATION", "tools/InterLan.FoundationChecks/InterLan.FoundationChecks.csproj"),
    ("P1 IDENTITY", "tools/InterLan.P1Checks/InterLan.P1Checks.csproj"),
    ("P1 NETWORK", "tools/InterLan.P1NetworkSmoke/InterLan.P1NetworkSmoke.csproj"),
    ("P2 CORE", "tools/InterLan.P2Checks/InterLan.P2Checks.csproj"),
    ("P2 REALTIME", "tools/InterLan.P2RealtimeSmoke/InterLan.P2RealtimeSmoke.csproj"),
    ("P2 CONCURRENCY", "tools/InterLan.P2ConcurrencyChecks/InterLan.P2ConcurrencyChecks.csproj"),
    ("P3 CORE", "tools/InterLan.P3Checks/InterLan.P3Checks.csproj"),
    ("P3 REALTIME", "tools/InterLan.P3RealtimeSmoke/InterLan.P3RealtimeSmoke.csproj"),
    ("P3 CONCURRENCY", "tools/InterLan.P3ConcurrencyChecks/InterLan.P3ConcurrencyChecks.csproj"),
    ("MIGRATIONS", "tools/InterLan.MigrationChecks/InterLan.MigrationChecks.csproj"),
    ("DESKTOP OWNER HOST", "tools/InterLan.DesktopChecks/InterLan.DesktopChecks.csproj")
};

foreach (var (name, relativeProject) in checks)
{
    Console.WriteLine();
    Console.WriteLine($"=== {name} ===");

    var projectPath = Path.Combine(
        repositoryRoot,
        relativeProject.Replace('/', Path.DirectorySeparatorChar));

    var startInfo = new ProcessStartInfo
    {
        FileName = "dotnet",
        WorkingDirectory = repositoryRoot,
        UseShellExecute = false,
        RedirectStandardOutput = true,
        RedirectStandardError = true
    };
    startInfo.ArgumentList.Add("run");
    startInfo.ArgumentList.Add("--project");
    startInfo.ArgumentList.Add(projectPath);
    startInfo.ArgumentList.Add("--configuration");
    startInfo.ArgumentList.Add("Release");
    startInfo.ArgumentList.Add("--no-build");

    using var process = new Process { StartInfo = startInfo };

    if (!process.Start())
    {
        Console.Error.WriteLine($"FAIL {name}: dotnet process did not start");
        return 1;
    }

    var standardOutput = process.StandardOutput.ReadToEndAsync();
    var standardError = process.StandardError.ReadToEndAsync();

    await process.WaitForExitAsync();

    var output = await standardOutput;
    var error = await standardError;

    if (!string.IsNullOrWhiteSpace(output))
        Console.Write(output);

    if (!string.IsNullOrWhiteSpace(error))
        Console.Error.Write(error);

    if (process.ExitCode != 0)
    {
        Console.Error.WriteLine(
            $"INTER-LAN SUITE CHECKS: FAIL at {name} (exit {process.ExitCode})");
        return process.ExitCode;
    }
}

Console.WriteLine();
Console.WriteLine("INTER-LAN P0-P3 AGGREGATE SUITE: PASS");
return 0;
