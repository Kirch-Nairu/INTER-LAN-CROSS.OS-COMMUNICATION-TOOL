namespace InterLan.Testing;

public static class RepositoryLayout
{
    public static string FindRoot(string? startDirectory = null)
    {
        var current = new DirectoryInfo(
            Path.GetFullPath(startDirectory ?? AppContext.BaseDirectory));

        while (current is not null)
        {
            if (File.Exists(Path.Combine(current.FullName, "InterLan.sln")) &&
                Directory.Exists(Path.Combine(current.FullName, "src")) &&
                Directory.Exists(Path.Combine(current.FullName, "tools")))
            {
                return current.FullName;
            }

            current = current.Parent;
        }

        throw new DirectoryNotFoundException(
            "Could not locate INTER-LAN repository root from the current process path.");
    }

    public static string ServerDll(string repositoryRoot) =>
        Path.Combine(
            Path.GetFullPath(repositoryRoot),
            "src",
            "InterLan.Server",
            "bin",
            "Release",
            "net10.0",
            "InterLan.Server.dll");
}
