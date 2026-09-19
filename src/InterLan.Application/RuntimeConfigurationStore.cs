using System.Text.Json;
using System.Text.Json.Serialization;

namespace InterLan.Application;

public sealed class RuntimeConfigurationStore
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        WriteIndented = true,
        Converters = { new JsonStringEnumConverter() }
    };

    public async Task<RuntimeConfiguration> LoadAsync(string path, CancellationToken cancellationToken = default)
    {
        if (!File.Exists(path))
        {
            return RuntimeConfiguration.Unconfigured;
        }

        await using var stream = File.OpenRead(path);
        return await JsonSerializer.DeserializeAsync<RuntimeConfiguration>(stream, JsonOptions, cancellationToken)
            ?? RuntimeConfiguration.Unconfigured;
    }

    public async Task SaveAsync(
        string path,
        RuntimeConfiguration configuration,
        CancellationToken cancellationToken = default)
    {
        var errors = configuration.Validate();
        if (errors.Count > 0)
        {
            throw new InvalidOperationException(string.Join(Environment.NewLine, errors));
        }

        var fullPath = Path.GetFullPath(path);
        var directory = Path.GetDirectoryName(fullPath);
        if (!string.IsNullOrWhiteSpace(directory))
        {
            Directory.CreateDirectory(directory);
        }

        var temporaryPath = fullPath + ".tmp";
        await using (var stream = File.Create(temporaryPath))
        {
            await JsonSerializer.SerializeAsync(stream, configuration, JsonOptions, cancellationToken);
        }

        File.Move(temporaryPath, fullPath, true);
    }
}
