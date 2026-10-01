namespace InterLan.Application.CrmHost;

public sealed record HostDependencyResult(
    string Name,
    bool Available,
    string? ResolvedPath,
    string Detail);

public static class HostDependencyProbe
{
    public static HostDependencyResult ProbeExecutable(
        string executable,
        string? pathVariable = null)
    {
        if (string.IsNullOrWhiteSpace(executable))
        {
            return new HostDependencyResult("executable", false, null, "Executable was not configured.");
        }

        executable = executable.Trim();

        if (Path.IsPathRooted(executable))
        {
            var fullPath = Path.GetFullPath(executable);
            return File.Exists(fullPath)
                ? new HostDependencyResult(Path.GetFileName(fullPath), true, fullPath, "Executable path exists.")
                : new HostDependencyResult(Path.GetFileName(fullPath), false, fullPath, "Configured executable path does not exist.");
        }

        var searchPath = pathVariable ?? Environment.GetEnvironmentVariable("PATH") ?? string.Empty;
        foreach (var directory in searchPath.Split(Path.PathSeparator, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            string candidate;
            try
            {
                candidate = Path.GetFullPath(Path.Combine(directory, executable));
            }
            catch (Exception ex) when (ex is ArgumentException or NotSupportedException or PathTooLongException)
            {
                continue;
            }

            if (File.Exists(candidate))
            {
                return new HostDependencyResult(executable, true, candidate, "Executable resolved from PATH.");
            }
        }

        return new HostDependencyResult(executable, false, null, "Executable was not found on PATH.");
    }
}
