using InterLan.Application;
using InterLan.Domain;
using Microsoft.Data.Sqlite;

namespace InterLan.Infrastructure;

public sealed class SqliteServerIdentityStore(SqliteDatabase database) : IServerIdentityStore
{
    public async Task<ServerIdentity?> GetAsync(CancellationToken cancellationToken = default)
    {
        await using var connection = database.OpenConnection();
        await using var command = connection.CreateCommand();
        command.CommandText =
            """
            SELECT server_id, server_name, owner_user_id, created_utc
            FROM server_identity
            WHERE singleton_key = 1;
            """;

        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        if (!await reader.ReadAsync(cancellationToken))
        {
            return null;
        }

        return new ServerIdentity(
            Guid.Parse(reader.GetString(0)),
            reader.GetString(1),
            Guid.Parse(reader.GetString(2)),
            DateTimeOffset.Parse(reader.GetString(3)));
    }

    public async Task CreateAsync(ServerIdentity identity, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(identity);

        await using var connection = database.OpenConnection();
        await using var command = connection.CreateCommand();
        command.CommandText =
            """
            INSERT INTO server_identity (
                singleton_key, server_id, server_name, owner_user_id, created_utc
            ) VALUES (
                1, $serverId, $serverName, $ownerUserId, $createdUtc
            );
            """;
        command.Parameters.AddWithValue("$serverId", identity.ServerId.ToString("D"));
        command.Parameters.AddWithValue("$serverName", identity.ServerName);
        command.Parameters.AddWithValue("$ownerUserId", identity.OwnerUserId.ToString("D"));
        command.Parameters.AddWithValue("$createdUtc", identity.CreatedUtc.ToString("O"));

        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    public async Task TransferOwnerAsync(
        Guid expectedCurrentOwnerUserId,
        Guid nextOwnerUserId,
        CancellationToken cancellationToken = default)
    {
        if (expectedCurrentOwnerUserId == Guid.Empty || nextOwnerUserId == Guid.Empty)
        {
            throw new ArgumentException("Owner IDs must be non-empty.");
        }

        await using var connection = database.OpenConnection();
        await using var command = connection.CreateCommand();
        command.CommandText =
            """
            UPDATE server_identity
            SET owner_user_id = $nextOwner
            WHERE singleton_key = 1 AND owner_user_id = $expectedOwner;
            """;
        command.Parameters.AddWithValue("$nextOwner", nextOwnerUserId.ToString("D"));
        command.Parameters.AddWithValue("$expectedOwner", expectedCurrentOwnerUserId.ToString("D"));

        var changed = await command.ExecuteNonQueryAsync(cancellationToken);
        if (changed != 1)
        {
            throw new UnauthorizedAccessException("Server ownership changed or the actor is not the current owner.");
        }
    }
}
