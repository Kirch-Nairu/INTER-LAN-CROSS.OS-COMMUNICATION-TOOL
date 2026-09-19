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
        var preservedPeerId = Guid.NewGuid();
        var preservedConversationId = Guid.NewGuid();
        var preservedMessageId = Guid.NewGuid();
        var preservedMessageClientId = Guid.NewGuid();
        var preservedDeviceId = Guid.NewGuid();
        var preservedGroupId = Guid.NewGuid();

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

            if (historicalMigration == "007_p2_direct_preferences")
            {
                var now = DateTimeOffset.UtcNow.ToString("O");

                await using var seedP2 = preUpgrade.CreateCommand();
                seedP2.CommandText =
                    """
                    INSERT INTO users (
                        user_id, username, display_name, role, created_utc
                    ) VALUES (
                        $peerId, 'preserved-p2-peer', 'Preserved P2 Peer',
                        'MEMBER', $utc
                    );

                    INSERT INTO direct_conversations (
                        conversation_id, created_utc, pair_key
                    ) VALUES (
                        $conversationId, $utc, 'preserved-p2-pair'
                    );

                    INSERT INTO direct_conversation_members (
                        conversation_id, user_id, joined_utc
                    ) VALUES
                        ($conversationId, $userId, $utc),
                        ($conversationId, $peerId, $utc);

                    INSERT INTO messages (
                        message_id, scope_type, scope_id, sender_user_id,
                        client_message_id, body, reply_to_message_id,
                        created_utc, edited_utc, deleted_utc
                    ) VALUES (
                        $messageId, 'DIRECT', $conversationId, $userId,
                        $clientMessageId, 'preserved-p2-message', NULL,
                        $utc, NULL, NULL
                    );

                    INSERT INTO message_receipts (
                        message_id, user_id, delivered_utc, read_utc
                    ) VALUES (
                        $messageId, $peerId, $utc, $utc
                    );

                    INSERT INTO direct_conversation_preferences (
                        conversation_id, user_id, pinned_utc,
                        muted_until_utc, archived_utc, updated_utc
                    ) VALUES (
                        $conversationId, $userId, $utc,
                        NULL, NULL, $utc
                    );

                    INSERT INTO devices (
                        device_id, user_id, device_name, platform,
                        certificate_fingerprint, approved_utc, revoked_utc,
                        last_seen_utc, credential_hash, credential_created_utc,
                        credential_rotated_utc, credential_last_used_utc
                    ) VALUES (
                        $deviceId, $userId, 'Preserved P2 Device', 'WINDOWS',
                        NULL, $utc, NULL, $utc,
                        'preserved-p2-credential-hash', $utc, NULL, $utc
                    );

                    INSERT INTO groups (
                        group_id, name, topic, created_by_user_id, created_utc
                    ) VALUES (
                        $groupId, 'Preserved Pre-P3 Group',
                        'existing before migration 008', $userId, $utc
                    );

                    INSERT INTO group_members (
                        group_id, user_id, group_role, joined_utc, removed_utc
                    ) VALUES (
                        $groupId, $userId, 'OWNER', $utc, NULL
                    );
                    """;
                seedP2.Parameters.AddWithValue("$peerId", preservedPeerId.ToString("D"));
                seedP2.Parameters.AddWithValue("$conversationId", preservedConversationId.ToString("D"));
                seedP2.Parameters.AddWithValue("$userId", preservedUserId.ToString("D"));
                seedP2.Parameters.AddWithValue("$messageId", preservedMessageId.ToString("D"));
                seedP2.Parameters.AddWithValue("$clientMessageId", preservedMessageClientId.ToString("D"));
                seedP2.Parameters.AddWithValue("$deviceId", preservedDeviceId.ToString("D"));
                seedP2.Parameters.AddWithValue("$groupId", preservedGroupId.ToString("D"));
                seedP2.Parameters.AddWithValue("$utc", now);
                await seedP2.ExecuteNonQueryAsync();
            }
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

        if (historicalMigration == "007_p2_direct_preferences")
        {
            await using var preservedP2 = connection.CreateCommand();
            preservedP2.CommandText =
                """
                SELECT CASE WHEN
                    EXISTS (
                        SELECT 1
                        FROM direct_conversations
                        WHERE conversation_id = $conversationId
                          AND pair_key = 'preserved-p2-pair'
                    )
                    AND (
                        SELECT COUNT(1)
                        FROM direct_conversation_members
                        WHERE conversation_id = $conversationId
                          AND user_id IN ($userId, $peerId)
                    ) = 2
                    AND EXISTS (
                        SELECT 1
                        FROM messages
                        WHERE message_id = $messageId
                          AND scope_type = 'DIRECT'
                          AND scope_id = $conversationId
                          AND sender_user_id = $userId
                          AND client_message_id = $clientMessageId
                          AND body = 'preserved-p2-message'
                          AND deleted_utc IS NULL
                    )
                    AND EXISTS (
                        SELECT 1
                        FROM message_receipts
                        WHERE message_id = $messageId
                          AND user_id = $peerId
                          AND delivered_utc IS NOT NULL
                          AND read_utc IS NOT NULL
                    )
                    AND EXISTS (
                        SELECT 1
                        FROM direct_conversation_preferences
                        WHERE conversation_id = $conversationId
                          AND user_id = $userId
                          AND pinned_utc IS NOT NULL
                          AND archived_utc IS NULL
                    )
                    AND EXISTS (
                        SELECT 1
                        FROM devices
                        WHERE device_id = $deviceId
                          AND user_id = $userId
                          AND credential_hash = 'preserved-p2-credential-hash'
                          AND credential_created_utc IS NOT NULL
                          AND credential_last_used_utc IS NOT NULL
                          AND revoked_utc IS NULL
                    )
                    AND EXISTS (
                        SELECT 1
                        FROM groups
                        WHERE group_id = $groupId
                          AND created_by_user_id = $userId
                          AND name = 'Preserved Pre-P3 Group'
                    )
                    AND EXISTS (
                        SELECT 1
                        FROM group_members
                        WHERE group_id = $groupId
                          AND user_id = $userId
                          AND group_role = 'OWNER'
                          AND removed_utc IS NULL
                    )
                THEN 1 ELSE 0 END;
                """;
            preservedP2.Parameters.AddWithValue(
                "$conversationId",
                preservedConversationId.ToString("D"));
            preservedP2.Parameters.AddWithValue(
                "$userId",
                preservedUserId.ToString("D"));
            preservedP2.Parameters.AddWithValue(
                "$peerId",
                preservedPeerId.ToString("D"));
            preservedP2.Parameters.AddWithValue(
                "$messageId",
                preservedMessageId.ToString("D"));
            preservedP2.Parameters.AddWithValue(
                "$clientMessageId",
                preservedMessageClientId.ToString("D"));
            preservedP2.Parameters.AddWithValue(
                "$deviceId",
                preservedDeviceId.ToString("D"));
            preservedP2.Parameters.AddWithValue(
                "$groupId",
                preservedGroupId.ToString("D"));

            Check(
                Convert.ToInt32(await preservedP2.ExecuteScalarAsync()) == 1,
                "007_p2_direct_preferences preserves P2 data through migration 008");
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
