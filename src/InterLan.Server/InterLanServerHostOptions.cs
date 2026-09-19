namespace InterLan.Server;

public sealed record InterLanServerHostOptions(
    string? DataDirectory = null,
    IReadOnlyDictionary<string, string?>? ConfigurationOverrides = null);
