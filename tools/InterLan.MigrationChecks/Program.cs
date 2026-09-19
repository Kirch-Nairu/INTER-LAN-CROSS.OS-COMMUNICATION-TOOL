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
    "004_p2_device_pairing_persistence",
    "005_p2_device_credential_lifecycle",
    "006_p2_query_indexes",
    "007_p2_direct_preferences"
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

        var preservedUserId = Guid.NewGuid();
        await using (var preUpgrade = database.OpenConnection())
        {
            await using var seed = preUpgrade.CreateCommand();
            seed.CommandText =
                """
                INSERT INTO users (
                    user_id, username, display_name, role, created_utc
                ) VALUES (
                    $id, $username, $displayName, 'MEMBER', $createdUtc
                );
                """;
            seed.Parameters.AddWithValue("$id", preservedUserId.ToString("D"));
            seed.Parameters.AddWithValue(
                "$username",
                $"preserved-{historicalMigration}");
            seed.Parameters.AddWithValue(
                "$displayName",
                $"Preserved {historicalMigration}");
            seed.Parameters.AddWithValue(
                "$createdUtc",
                DateTimeOffset.UtcNow.ToString("O"));
            await seed.ExecuteNonQueryAsync();
        }

        await database.InitializeAsync();

        await using var connection = database.OpenConnection();

        await using (var history = connection.CreateCommand())
        {
            history.CommandText = "SELECT COUNT(1) FROM schema_migrations;";
            var count = Convert.ToInt32(await history.ExecuteScalarAsync());
            Check(
                count == 8,
                $"{historicalMigration} upgrades through all current migrations");
        }

        await using (var preserved = connection.CreateCommand())
        {
            preserved.CommandText =
                "SELECT COUNT(1) FROM users WHERE user_id = $id;";
            preserved.Parameters.AddWithValue("$id", preservedUserId.ToString("D"));
            Check(
                Convert.ToInt32(await preserved.ExecuteScalarAsync()) == 1,
                $"{historicalMigration} preserves pre-upgrade user data");
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

        await using (var preferencesTable = connection.CreateCommand())
        {
            preferencesTable.CommandText =
                """
                SELECT COUNT(1)
                FROM sqlite_master
                WHERE type = 'table'
                  AND name = 'direct_conversation_preferences';
                """;

            Check(
                Convert.ToInt32(await preferencesTable.ExecuteScalarAsync()) == 1,
                $"{historicalMigration} upgrades direct conversation preference schema");
        }

        await using (var groupEventsTable = connection.CreateCommand())
        {
            groupEventsTable.CommandText =
                """
                SELECT COUNT(1)
                FROM sqlite_master
                WHERE type = 'table'
                  AND name = 'group_events';
                """;

            Check(
                Convert.ToInt32(await groupEventsTable.ExecuteScalarAsync()) == 1,
                $"{historicalMigration} upgrades durable group event schema");
        }

        var requiredIndexes = new[]
        {
            "ix_direct_conversation_members_user",
            "ix_messages_scope_order",
            "ix_message_receipts_user_read",
            "ix_devices_user_revoked",
            "ix_device_sessions_device_active",
            "ix_group_events_group_created",
            "ix_group_members_user_active",
            "ix_group_members_group_active"
        };

        foreach (var indexName in requiredIndexes)
        {
            await using var index = connection.CreateCommand();
            index.CommandText =
                """
                SELECT COUNT(1)
                FROM sqlite_master
                WHERE type = 'index'
                  AND name = $name;
                """;
            index.Parameters.AddWithValue("$name", indexName);

            Check(
                Convert.ToInt32(await index.ExecuteScalarAsync()) == 1,
                $"{historicalMigration} upgrades required index {indexName}");
        }

        await database.InitializeAsync();

        await using var idempotentConnection = database.OpenConnection();
        await using var idempotentHistory = idempotentConnection.CreateCommand();
        idempotentHistory.CommandText = "SELECT COUNT(1) FROM schema_migrations;";
        Check(
            Convert.ToInt32(await idempotentHistory.ExecuteScalarAsync()) == 8,
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
