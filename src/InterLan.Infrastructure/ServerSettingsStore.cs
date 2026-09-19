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

    public async Task SaveAsync(
        ServerRuntimeSettings settings,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(settings);

        var errors = settings.Validate();
        if (errors.Count > 0)
            throw new ArgumentException(
                string.Join("; ", errors),
                nameof(settings));

        var now = DateTimeOffset.UtcNow.ToString("O");

        await using var connection = database.OpenConnection();
        await using var command = connection.CreateCommand();
        command.CommandText =
            """
            INSERT INTO server_settings (
                singleton_key, bind_address, port, discovery_enabled,
                client_approval_required, storage_path, created_utc, updated_utc
            ) VALUES (
                1, $bindAddress, $port, $discoveryEnabled,
                $approvalRequired, $storagePath, $now, $now
            )
            ON CONFLICT(singleton_key) DO UPDATE SET
                bind_address = excluded.bind_address,
                port = excluded.port,
                discovery_enabled = excluded.discovery_enabled,
                client_approval_required = excluded.client_approval_required,
                storage_path = excluded.storage_path,
                updated_utc = excluded.updated_utc;
            """;
        command.Parameters.AddWithValue("$bindAddress", settings.BindAddress);
        command.Parameters.AddWithValue("$port", settings.Port);
        command.Parameters.AddWithValue("$discoveryEnabled", settings.DiscoveryEnabled ? 1 : 0);
        command.Parameters.AddWithValue("$approvalRequired", settings.ClientApprovalRequired ? 1 : 0);
        command.Parameters.AddWithValue("$storagePath", settings.StoragePath);
        command.Parameters.AddWithValue("$now", now);

        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    public async Task<ServerRuntimeSettings> LoadAsync(
        string dataDirectory,
        CancellationToken cancellationToken = default)
    {
        var defaults = ServerRuntimeSettings.CreateDefaults(dataDirectory);

        return await LoadPersistedAsync(cancellationToken) ?? defaults;
    }
}
