using InterLan.Infrastructure;

namespace InterLan.Testing;

public static class HistoricalDatabaseFixture
{
    public static async Task<SqliteDatabase> CreateThroughAsync(
        string databasePath,
        string lastMigrationId,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(databasePath);
        ArgumentException.ThrowIfNullOrWhiteSpace(lastMigrationId);

        var database = new SqliteDatabase(databasePath);
        await using var connection = database.OpenConnection();

        await using (var history = connection.CreateCommand())
        {
            history.CommandText =
                """
                CREATE TABLE IF NOT EXISTS schema_migrations (
                    migration_id TEXT PRIMARY KEY,
                    applied_utc TEXT NOT NULL
                );
                """;
            await history.ExecuteNonQueryAsync(cancellationToken);
        }

        var assembly = typeof(SqliteDatabase).Assembly;
        var resources = assembly.GetManifestResourceNames()
            .Where(name =>
                name.Contains(".Migrations.", StringComparison.Ordinal) &&
                name.EndsWith(".sql", StringComparison.Ordinal))
            .OrderBy(name => name, StringComparer.Ordinal)
            .ToArray();

        var reachedTarget = false;

        foreach (var resource in resources)
        {
            var migrationId = GetMigrationId(resource);

            await using var stream = assembly.GetManifestResourceStream(resource)
                ?? throw new InvalidOperationException(
                    $"Embedded migration not found: {resource}");
            using var reader = new StreamReader(stream);
            var sql = await reader.ReadToEndAsync(cancellationToken);

            using var transaction = connection.BeginTransaction();

            await using (var migration = connection.CreateCommand())
            {
                migration.Transaction = transaction;
                migration.CommandText = sql;
                await migration.ExecuteNonQueryAsync(cancellationToken);
            }

            await using (var record = connection.CreateCommand())
            {
                record.Transaction = transaction;
                record.CommandText =
                    """
                    INSERT INTO schema_migrations (migration_id, applied_utc)
                    VALUES ($id, $utc);
                    """;
                record.Parameters.AddWithValue("$id", migrationId);
                record.Parameters.AddWithValue(
                    "$utc",
                    DateTimeOffset.UtcNow.ToString("O"));
                await record.ExecuteNonQueryAsync(cancellationToken);
            }

            transaction.Commit();

            if (string.Equals(
                migrationId,
                lastMigrationId,
                StringComparison.Ordinal))
            {
                reachedTarget = true;
                break;
            }
        }

        if (!reachedTarget)
        {
            throw new ArgumentOutOfRangeException(
                nameof(lastMigrationId),
                lastMigrationId,
                "Requested historical migration does not exist.");
        }

        return database;
    }

    private static string GetMigrationId(string resource)
    {
        const string marker = ".Migrations.";
        var index = resource.LastIndexOf(marker, StringComparison.Ordinal);
        if (index < 0)
            throw new InvalidDataException(
                $"Migration resource has an invalid name: {resource}");

        return resource[(index + marker.Length)..^4];
    }
}
