namespace InterLan.Application.CrmHost;

public sealed record LinuxHostPaths(
    string ConfigDirectory,
    string StateDirectory,
    string DataDirectory,
    string CacheDirectory,
    string RuntimeDirectory,
    string DatabasePath,
    string EvidenceDirectory,
    string ArtifactDirectory,
    string BackupDirectory,
    string LogDirectory,
    string LockFilePath,
    string RuntimeStatePath)
{
    public static LinuxHostPaths Resolve(
        IReadOnlyDictionary<string, string?>? environment = null,
        string? homeDirectory = null)
    {
        environment ??= Environment.GetEnvironmentVariables()
            .Cast<System.Collections.DictionaryEntry>()
            .ToDictionary(
                entry => Convert.ToString(entry.Key) ?? string.Empty,
                entry => Convert.ToString(entry.Value),
                StringComparer.Ordinal);

        var home = NormalizeAbsolute(
            homeDirectory ?? Read(environment, "HOME") ?? Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
            "HOME");

        var configRoot = ResolveRoot(environment, "XDG_CONFIG_HOME", Path.Combine(home, ".config"));
        var stateRoot = ResolveRoot(environment, "XDG_STATE_HOME", Path.Combine(home, ".local", "state"));
        var dataRoot = ResolveRoot(environment, "XDG_DATA_HOME", Path.Combine(home, ".local", "share"));
        var cacheRoot = ResolveRoot(environment, "XDG_CACHE_HOME", Path.Combine(home, ".cache"));
        var runtimeRoot = ResolveRuntimeRoot(environment, stateRoot);

        var config = Path.Combine(configRoot, "interlan");
        var state = Path.Combine(stateRoot, "interlan");
        var data = Path.Combine(dataRoot, "interlan");
        var cache = Path.Combine(cacheRoot, "interlan");
        var runtime = Path.Combine(runtimeRoot, "interlan");

        return new LinuxHostPaths(
            config,
            state,
            data,
            cache,
            runtime,
            Path.Combine(data, "interlan.db"),
            Path.Combine(data, "evidence"),
            Path.Combine(data, "artifacts"),
            Path.Combine(data, "backups"),
            Path.Combine(state, "logs"),
            Path.Combine(runtime, "host.lock"),
            Path.Combine(runtime, "runtime-state.json"));
    }

    public IEnumerable<string> RequiredDirectories()
    {
        yield return ConfigDirectory;
        yield return StateDirectory;
        yield return DataDirectory;
        yield return CacheDirectory;
        yield return RuntimeDirectory;
        yield return EvidenceDirectory;
        yield return ArtifactDirectory;
        yield return BackupDirectory;
        yield return LogDirectory;
    }

    private static string ResolveRoot(
        IReadOnlyDictionary<string, string?> environment,
        string variable,
        string fallback) =>
        NormalizeAbsolute(Read(environment, variable) ?? fallback, variable);

    private static string ResolveRuntimeRoot(
        IReadOnlyDictionary<string, string?> environment,
        string stateRoot)
    {
        var configured = Read(environment, "XDG_RUNTIME_DIR");
        return configured is null
            ? Path.Combine(stateRoot, "runtime")
            : NormalizeAbsolute(configured, "XDG_RUNTIME_DIR");
    }

    private static string? Read(IReadOnlyDictionary<string, string?> environment, string key) =>
        environment.TryGetValue(key, out var value) && !string.IsNullOrWhiteSpace(value)
            ? value.Trim()
            : null;

    private static string NormalizeAbsolute(string path, string source)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            throw new InvalidOperationException($"{source} resolved to an empty path.");
        }

        if (!Path.IsPathRooted(path))
        {
            throw new InvalidOperationException($"{source} must resolve to an absolute path.");
        }

        return Path.GetFullPath(path);
    }
}
