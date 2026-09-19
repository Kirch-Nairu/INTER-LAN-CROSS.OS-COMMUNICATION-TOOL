using InterLan.Application;

namespace InterLan.Infrastructure;

public sealed class ServerSettingsStore(SqliteDatabase database)
{
    public async Task<ServerRuntimeSettings?> LoadPersistedAsync(
        CancellationToken cancellationToken = default)
    {
        await using var connection = database.OpenConnection();
        await using var command = connection.CreateCommand();
        command.CommandText =
            """
            SELECT bind_address, port, discovery_enabled,
                   client_approval_required, storage_path
            FROM server_settings
            WHERE singleton_key = 1;
            """;

        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        if (!await reader.ReadAsync(cancellationToken))
            return null;

        var settings = new ServerRuntimeSettings(
            reader.GetString(0),
            reader.GetInt32(1),
            reader.GetInt32(2) == 1,
            reader.GetInt32(3) == 1,
            reader.GetString(4));

        var errors = settings.Validate();
        if (errors.Count > 0)
            throw new InvalidDataException(
                $"Persisted server settings are invalid: {string.Join("; ", errors)}");

        return settings;
    }

    public async Task<ServerRuntimeSettings> LoadAsync(
        string dataDirectory,
        CancellationToken cancellationToken = default)
    {
        var defaults = ServerRuntimeSettings.CreateDefaults(dataDirectory);

        await using var connection = database.OpenConnection();
        await using var command = connection.CreateCommand();
        command.CommandText =
            """
            SELECT bind_address, port, discovery_enabled,
                   client_approval_required, storage_path
            FROM server_settings
            WHERE singleton_key = 1;
            """;

        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        if (!await reader.ReadAsync(cancellationToken))
            return defaults;

        var settings = new ServerRuntimeSettings(
            reader.GetString(0),
            reader.GetInt32(1),
            reader.GetInt32(2) == 1,
            reader.GetInt32(3) == 1,
            reader.GetString(4));

        var errors = settings.Validate();
        if (errors.Count > 0)
            throw new InvalidDataException(
                $"Persisted server settings are invalid: {string.Join("; ", errors)}");

        return settings;
    }
}
