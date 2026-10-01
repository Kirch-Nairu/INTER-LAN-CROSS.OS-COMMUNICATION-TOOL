using System.Text.Json;

namespace InterLan.Application.CrmHost;

public sealed record RuntimeStateEnvelope(
    int SchemaVersion,
    HostRuntimeSnapshot Snapshot,
    IReadOnlyDictionary<string, int> ProcessIds,
    DateTimeOffset WrittenAt);

public sealed class RuntimeStateStore
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        WriteIndented = true
    };

    private readonly string _statePath;

    public RuntimeStateStore(string statePath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(statePath);
        _statePath = Path.GetFullPath(statePath);
    }

    public async Task WriteAsync(
        HostRuntimeSnapshot snapshot,
        IReadOnlyDictionary<string, int> processIds,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(snapshot);
        ArgumentNullException.ThrowIfNull(processIds);

        var directory = Path.GetDirectoryName(_statePath)!;
        Directory.CreateDirectory(directory);

        var envelope = new RuntimeStateEnvelope(1, snapshot, processIds, DateTimeOffset.UtcNow);
        var temporaryPath = $"{_statePath}.{Guid.NewGuid():N}.tmp";

        try
        {
            await using (var stream = new FileStream(
                temporaryPath,
                FileMode.CreateNew,
                FileAccess.Write,
                FileShare.None,
                bufferSize: 4096,
                FileOptions.Asynchronous | FileOptions.WriteThrough))
            {
                await JsonSerializer.SerializeAsync(stream, envelope, JsonOptions, cancellationToken);
                await stream.FlushAsync(cancellationToken);
            }

            File.Move(temporaryPath, _statePath, overwrite: true);
        }
        finally
        {
            try
            {
                File.Delete(temporaryPath);
            }
            catch (IOException)
            {
            }
        }
    }

    public async Task<RuntimeStateEnvelope?> ReadAsync(CancellationToken cancellationToken = default)
    {
        if (!File.Exists(_statePath))
        {
            return null;
        }

        try
        {
            await using var stream = new FileStream(
                _statePath,
                FileMode.Open,
                FileAccess.Read,
                FileShare.Read,
                bufferSize: 4096,
                FileOptions.Asynchronous | FileOptions.SequentialScan);

            var state = await JsonSerializer.DeserializeAsync<RuntimeStateEnvelope>(stream, JsonOptions, cancellationToken);
            return state?.SchemaVersion == 1 ? state : null;
        }
        catch (JsonException)
        {
            return null;
        }
        catch (IOException)
        {
            return null;
        }
    }

    public void Delete()
    {
        try
        {
            File.Delete(_statePath);
        }
        catch (IOException)
        {
        }
    }
}
