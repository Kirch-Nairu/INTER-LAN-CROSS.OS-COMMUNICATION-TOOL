using System.Reflection;
using Microsoft.Data.Sqlite;

namespace InterLan.Infrastructure;

public sealed class SqliteDatabase
{
    public SqliteDatabase(string databasePath)
    {
        DatabasePath = Path.GetFullPath(databasePath);
        var builder = new SqliteConnectionStringBuilder
        {
            DataSource = DatabasePath,
            Mode = SqliteOpenMode.ReadWriteCreate,
            Cache = SqliteCacheMode.Shared
        };
        ConnectionString = builder.ToString();
    }

    public string DatabasePath { get; }
    public string ConnectionString { get; }

    public SqliteConnection OpenConnection()
    {
        var directory = Path.GetDirectoryName(DatabasePath);
        if (!string.IsNullOrWhiteSpace(directory))
        {
            Directory.CreateDirectory(directory);
        }

        var connection = new SqliteConnection(ConnectionString);
        connection.Open();

        using var pragma = connection.CreateCommand();
        pragma.CommandText = "PRAGMA foreign_keys = ON;";
        pragma.ExecuteNonQuery();

        return connection;
    }

    public async Task InitializeAsync(CancellationToken cancellationToken = default)
    {
        await using var connection = OpenConnection();
        await using var createHistory = connection.CreateCommand();
        createHistory.CommandText =
            """
            CREATE TABLE IF NOT EXISTS schema_migrations (
                migration_id TEXT PRIMARY KEY,
                applied_utc TEXT NOT NULL
            );
            """;
        await createHistory.ExecuteNonQueryAsync(cancellationToken);

        var assembly = typeof(SqliteDatabase).Assembly;
        var resources = assembly.GetManifestResourceNames()
            .Where(name => name.Contains(".Migrations.", StringComparison.Ordinal) && name.EndsWith(".sql", StringComparison.Ordinal))
            .OrderBy(name => name, StringComparer.Ordinal)
            .ToArray();

        foreach (var resource in resources)
        {
            var migrationId = resource[(resource.LastIndexOf(".Migrations.", StringComparison.Ordinal) + ".Migrations.".Length)..^4];

            await using var exists = connection.CreateCommand();
            exists.CommandText = "SELECT COUNT(1) FROM schema_migrations WHERE migration_id = $id;";
            exists.Parameters.AddWithValue("$id", migrationId);
            var applied = Convert.ToInt64(await exists.ExecuteScalarAsync(cancellationToken)) > 0;
            if (applied)
            {
                continue;
            }

            await using var stream = assembly.GetManifestResourceStream(resource)
                ?? throw new InvalidOperationException($"Embedded migration not found: {resource}");
            using var reader = new StreamReader(stream);
            var sql = await reader.ReadToEndAsync(cancellationToken);

            await using var transaction = await connection.BeginTransactionAsync(cancellationToken);
            await using var migration = connection.CreateCommand();
            migration.Transaction = transaction;
            migration.CommandText = sql;
            await migration.ExecuteNonQueryAsync(cancellationToken);

            await using var record = connection.CreateCommand();
            record.Transaction = transaction;
            record.CommandText =
                "INSERT INTO schema_migrations (migration_id, applied_utc) VALUES ($id, $utc);";
            record.Parameters.AddWithValue("$id", migrationId);
            record.Parameters.AddWithValue("$utc", DateTimeOffset.UtcNow.ToString("O"));
            await record.ExecuteNonQueryAsync(cancellationToken);

            await transaction.CommitAsync(cancellationToken);
        }
    }
}
