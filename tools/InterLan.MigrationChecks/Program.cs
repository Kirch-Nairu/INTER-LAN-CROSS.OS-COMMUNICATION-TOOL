using InterLan.Infrastructure;
using InterLan.Testing;

var failures = new List<string>();

void Check(bool condition, string name)
{
    Console.WriteLine($"{(condition ? "PASS" : "FAIL")} {name}");
    if (!condition) failures.Add(name);
}

var historicalMigrations = new[]
{
    "001_initial",
    "002_p1_identity_enrollment",
    "003_p2_direct_messages",
    "004_p2_device_pairing_persistence"
};

foreach (var historicalMigration in historicalMigrations)
{
    var root = Path.Combine(
        Path.GetTempPath(),
        "interlan-migration-" + historicalMigration + "-" + Guid.NewGuid().ToString("N"));
    Directory.CreateDirectory(root);

    try
    {
        var databasePath = Path.Combine(root, "upgrade.db");
        var database = await HistoricalDatabaseFixture.CreateThroughAsync(
            databasePath,
            historicalMigration);

        await database.InitializeAsync();

        await using var connection = database.OpenConnection();

        await using (var history = connection.CreateCommand())
        {
            history.CommandText = "SELECT COUNT(1) FROM schema_migrations;";
            var count = Convert.ToInt32(await history.ExecuteScalarAsync());
            Check(
                count == 5,
                $"{historicalMigration} upgrades through all current migrations");
        }

        var deviceColumns = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        await using (var deviceInfo = connection.CreateCommand())
        {
            deviceInfo.CommandText = "PRAGMA table_info(devices);";
            await using var reader = await deviceInfo.ExecuteReaderAsync();
            while (await reader.ReadAsync())
                deviceColumns.Add(reader.GetString(1));
        }

        Check(
            deviceColumns.Contains("credential_hash") &&
            deviceColumns.Contains("credential_created_utc") &&
            deviceColumns.Contains("credential_rotated_utc") &&
            deviceColumns.Contains("credential_last_used_utc"),
            $"{historicalMigration} upgrades device credential schema");

        var directColumns = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        await using (var directInfo = connection.CreateCommand())
        {
            directInfo.CommandText = "PRAGMA table_info(direct_conversations);";
            await using var reader = await directInfo.ExecuteReaderAsync();
            while (await reader.ReadAsync())
                directColumns.Add(reader.GetString(1));
        }

        Check(
            directColumns.Contains("pair_key"),
            $"{historicalMigration} upgrades canonical DM schema");

        await database.InitializeAsync();

        await using var idempotentConnection = database.OpenConnection();
        await using var idempotentHistory = idempotentConnection.CreateCommand();
        idempotentHistory.CommandText = "SELECT COUNT(1) FROM schema_migrations;";
        Check(
            Convert.ToInt32(await idempotentHistory.ExecuteScalarAsync()) == 5,
            $"{historicalMigration} migration replay remains idempotent");
    }
    finally
    {
        try { Directory.Delete(root, recursive: true); } catch { }
    }
}

if (failures.Count > 0)
{
    Console.Error.WriteLine(
        $"Migration checks failed: {string.Join(", ", failures)}");
    return 1;
}

Console.WriteLine("INTER-LAN MIGRATION CHECKS: PASS");
return 0;
