using InterLan.Application;
using InterLan.Contracts;
using InterLan.Domain;
using Microsoft.Data.Sqlite;

namespace InterLan.Infrastructure;

public sealed class EnrollmentStore(SqliteDatabase database)
{
    public async Task<ServerIdentity> BootstrapOwnerAsync(
        string serverName,
        string username,
        string displayName,
        string password,
        string storagePath,
        int port,
        bool discoveryEnabled,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(serverName)) throw new ArgumentException("Server name is required.");
        if (string.IsNullOrWhiteSpace(username)) throw new ArgumentException("Owner username is required.");
        if (string.IsNullOrWhiteSpace(displayName)) throw new ArgumentException("Owner display name is required.");
        if (port is < 1 or > 65535) throw new ArgumentOutOfRangeException(nameof(port));

        var passwordMaterial = SecretCodec.HashPassword(password);
        var ownerId = Guid.NewGuid();
        var serverId = Guid.NewGuid();
        var now = DateTimeOffset.UtcNow;
        var identity = new ServerIdentity(serverId, serverName.Trim(), ownerId, now);

        await using var connection = database.OpenConnection();
        using var transaction = connection.BeginTransaction();

        await using (var configured = connection.CreateCommand())
        {
            configured.Transaction = transaction;
            configured.CommandText = "SELECT COUNT(1) FROM server_identity WHERE singleton_key = 1;";
            var count = Convert.ToInt64(await configured.ExecuteScalarAsync(cancellationToken));
            if (count != 0)
                throw new InvalidOperationException("Server is already configured.");
        }

        await using (var user = connection.CreateCommand())
        {
            user.Transaction = transaction;
            user.CommandText =
                """
                INSERT INTO users (
                    user_id, username, display_name, role, created_utc,
                    password_salt, password_hash, password_iterations
                ) VALUES (
                    $id, $username, $display, 'OWNER', $utc,
                    $salt, $hash, $iterations
                );
                """;
            user.Parameters.AddWithValue("$id", ownerId.ToString("D"));
            user.Parameters.AddWithValue("$username", username.Trim());
            user.Parameters.AddWithValue("$display", displayName.Trim());
            user.Parameters.AddWithValue("$utc", now.ToString("O"));
            user.Parameters.AddWithValue("$salt", passwordMaterial.Salt);
            user.Parameters.AddWithValue("$hash", passwordMaterial.Hash);
            user.Parameters.AddWithValue("$iterations", passwordMaterial.Iterations);
            await user.ExecuteNonQueryAsync(cancellationToken);
        }

        await using (var server = connection.CreateCommand())
        {
            server.Transaction = transaction;
            server.CommandText =
                """
                INSERT INTO server_identity (
                    singleton_key, server_id, server_name, owner_user_id, created_utc
                ) VALUES (1, $serverId, $serverName, $ownerId, $utc);
                """;
            server.Parameters.AddWithValue("$serverId", serverId.ToString("D"));
            server.Parameters.AddWithValue("$serverName", serverName.Trim());
            server.Parameters.AddWithValue("$ownerId", ownerId.ToString("D"));
            server.Parameters.AddWithValue("$utc", now.ToString("O"));
            await server.ExecuteNonQueryAsync(cancellationToken);
        }

        await using (var settings = connection.CreateCommand())
        {
            settings.Transaction = transaction;
            settings.CommandText =
                """
                INSERT INTO server_settings (
                    singleton_key, bind_address, port, discovery_enabled,
                    client_approval_required, storage_path, created_utc, updated_utc
                ) VALUES (
                    1, '0.0.0.0', $port, $discovery, 1, $storage, $utc, $utc
                );
                """;
            settings.Parameters.AddWithValue("$port", port);
            settings.Parameters.AddWithValue("$discovery", discoveryEnabled ? 1 : 0);
            settings.Parameters.AddWithValue("$storage", Path.GetFullPath(storagePath));
            settings.Parameters.AddWithValue("$utc", now.ToString("O"));
            await settings.ExecuteNonQueryAsync(cancellationToken);
        }

        await AppendAuditAsync(connection, transaction, ownerId, "SERVER_BOOTSTRAPPED", "SERVER", serverId, "{}", now, cancellationToken);
        transaction.Commit();
        return identity;
    }

    public async Task<SessionResponse> LoginOwnerAsync(
        string username,
        string password,
        TimeSpan lifetime,
        CancellationToken cancellationToken = default)
    {
        await using var connection = database.OpenConnection();
        await using var command = connection.CreateCommand();
        command.CommandText =
            """
            SELECT user_id, role, password_salt, password_hash, password_iterations, disabled_utc
            FROM users
            WHERE username = $username;
            """;
        command.Parameters.AddWithValue("$username", username.Trim());

        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        if (!await reader.ReadAsync(cancellationToken) || !reader.IsDBNull(5))
            throw new UnauthorizedAccessException("Invalid credentials.");

        var role = reader.GetString(1);
        if (role != "OWNER")
            throw new UnauthorizedAccessException("Owner authentication required.");

        var salt = reader.IsDBNull(2) ? null : reader.GetString(2);
        var hash = reader.IsDBNull(3) ? null : reader.GetString(3);
        var iterations = reader.IsDBNull(4) ? 0 : reader.GetInt32(4);
        if (salt is null || hash is null || !SecretCodec.VerifyPassword(password, salt, hash, iterations))
            throw new UnauthorizedAccessException("Invalid credentials.");

        var userId = Guid.Parse(reader.GetString(0));
        await reader.DisposeAsync();
        return await CreateSessionAsync(connection, userId, null, role, lifetime, cancellationToken);
    }

    public async Task<InviteResponse> CreateInviteAsync(
        Guid ownerUserId,
        TimeSpan lifetime,
        CancellationToken cancellationToken = default)
    {
        if (lifetime <= TimeSpan.Zero || lifetime > TimeSpan.FromDays(7))
            throw new ArgumentOutOfRangeException(nameof(lifetime));

        await using var connection = database.OpenConnection();
        await RequireOwnerAsync(connection, ownerUserId, cancellationToken);

        var inviteId = Guid.NewGuid();
        var token = SecretCodec.NewToken();
        var now = DateTimeOffset.UtcNow;
        var expires = now.Add(lifetime);

        await using var command = connection.CreateCommand();
        command.CommandText =
            """
            INSERT INTO invite_tokens (
                invite_id, token_hash, created_by_user_id, expires_utc, created_utc
            ) VALUES ($id, $hash, $owner, $expires, $created);
            """;
        command.Parameters.AddWithValue("$id", inviteId.ToString("D"));
        command.Parameters.AddWithValue("$hash", SecretCodec.HashToken(token));
        command.Parameters.AddWithValue("$owner", ownerUserId.ToString("D"));
        command.Parameters.AddWithValue("$expires", expires.ToString("O"));
        command.Parameters.AddWithValue("$created", now.ToString("O"));
        await command.ExecuteNonQueryAsync(cancellationToken);

        return new InviteResponse(inviteId, token, expires);
    }

    public async Task<SubmitJoinResponse> SubmitJoinAsync(
        SubmitJoinRequest request,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(request.Username) ||
            string.IsNullOrWhiteSpace(request.DisplayName) ||
            string.IsNullOrWhiteSpace(request.DeviceName) ||
            string.IsNullOrWhiteSpace(request.Platform) ||
            string.IsNullOrWhiteSpace(request.EnrollmentSecret))
            throw new ArgumentException("Join request is incomplete.");

        await using var connection = database.OpenConnection();
        using var transaction = connection.BeginTransaction();
        var now = DateTimeOffset.UtcNow;
        var inviteHash = SecretCodec.HashToken(request.InviteToken);

        Guid inviteId;
        await using (var invite = connection.CreateCommand())
        {
            invite.Transaction = transaction;
            invite.CommandText =
                """
                SELECT invite_id, expires_utc, consumed_utc
                FROM invite_tokens
                WHERE token_hash = $hash;
                """;
            invite.Parameters.AddWithValue("$hash", inviteHash);
            await using var reader = await invite.ExecuteReaderAsync(cancellationToken);
            if (!await reader.ReadAsync(cancellationToken))
                throw new UnauthorizedAccessException("Invite is invalid.");

            inviteId = Guid.Parse(reader.GetString(0));
            var expires = DateTimeOffset.Parse(reader.GetString(1));
            if (!reader.IsDBNull(2) || expires <= now)
                throw new UnauthorizedAccessException("Invite is expired or already consumed.");
        }

        await using (var consume = connection.CreateCommand())
        {
            consume.Transaction = transaction;
            consume.CommandText =
                """
                UPDATE invite_tokens
                SET consumed_utc = $utc
                WHERE invite_id = $id AND consumed_utc IS NULL;
                """;
            consume.Parameters.AddWithValue("$utc", now.ToString("O"));
            consume.Parameters.AddWithValue("$id", inviteId.ToString("D"));
            if (await consume.ExecuteNonQueryAsync(cancellationToken) != 1)
                throw new UnauthorizedAccessException("Invite is already consumed.");
        }

        var requestId = Guid.NewGuid();
        await using (var join = connection.CreateCommand())
        {
            join.Transaction = transaction;
            join.CommandText =
                """
                INSERT INTO join_requests (
                    request_id, invite_id, requested_username, display_name,
                    device_name, platform, enrollment_secret_hash, status, created_utc
                ) VALUES (
                    $requestId, $inviteId, $username, $displayName,
                    $deviceName, $platform, $secretHash, 'PENDING', $created
                );
                """;
            join.Parameters.AddWithValue("$requestId", requestId.ToString("D"));
            join.Parameters.AddWithValue("$inviteId", inviteId.ToString("D"));
            join.Parameters.AddWithValue("$username", request.Username.Trim());
            join.Parameters.AddWithValue("$displayName", request.DisplayName.Trim());
            join.Parameters.AddWithValue("$deviceName", request.DeviceName.Trim());
            join.Parameters.AddWithValue("$platform", request.Platform.Trim());
            join.Parameters.AddWithValue("$secretHash", SecretCodec.HashToken(request.EnrollmentSecret));
            join.Parameters.AddWithValue("$created", now.ToString("O"));
            await join.ExecuteNonQueryAsync(cancellationToken);
        }

        transaction.Commit();
        return new SubmitJoinResponse(requestId, "PENDING");
    }

    public async Task<JoinDecisionResponse> DecideJoinAsync(
        Guid ownerUserId,
        Guid requestId,
        bool approve,
        CancellationToken cancellationToken = default)
    {
        await using var connection = database.OpenConnection();
        await RequireOwnerAsync(connection, ownerUserId, cancellationToken);
        using var transaction = connection.BeginTransaction();
        var now = DateTimeOffset.UtcNow;

        string username;
        string displayName;
        string deviceName;
        string platform;

        await using (var request = connection.CreateCommand())
        {
            request.Transaction = transaction;
            request.CommandText =
                """
                SELECT requested_username, display_name, device_name, platform, status
                FROM join_requests
                WHERE request_id = $id;
                """;
            request.Parameters.AddWithValue("$id", requestId.ToString("D"));
            await using var reader = await request.ExecuteReaderAsync(cancellationToken);
            if (!await reader.ReadAsync(cancellationToken))
                throw new KeyNotFoundException("Join request not found.");
            if (reader.GetString(4) != "PENDING")
                throw new InvalidOperationException("Join request is already decided.");

            username = reader.GetString(0);
            displayName = reader.GetString(1);
            deviceName = reader.GetString(2);
            platform = reader.GetString(3);
        }

        if (!approve)
        {
            await using var reject = connection.CreateCommand();
            reject.Transaction = transaction;
            reject.CommandText =
                """
                UPDATE join_requests
                SET status = 'REJECTED', decided_by_user_id = $owner, decided_utc = $utc
                WHERE request_id = $id AND status = 'PENDING';
                """;
            reject.Parameters.AddWithValue("$owner", ownerUserId.ToString("D"));
            reject.Parameters.AddWithValue("$utc", now.ToString("O"));
            reject.Parameters.AddWithValue("$id", requestId.ToString("D"));
            await reject.ExecuteNonQueryAsync(cancellationToken);
            transaction.Commit();
            return new JoinDecisionResponse(requestId, "REJECTED", null, null);
        }

        var userId = Guid.NewGuid();
        var deviceId = Guid.NewGuid();

        await using (var user = connection.CreateCommand())
        {
            user.Transaction = transaction;
            user.CommandText =
                """
                INSERT INTO users (user_id, username, display_name, role, created_utc)
                VALUES ($id, $username, $display, 'MEMBER', $utc);
                """;
            user.Parameters.AddWithValue("$id", userId.ToString("D"));
            user.Parameters.AddWithValue("$username", username);
            user.Parameters.AddWithValue("$display", displayName);
            user.Parameters.AddWithValue("$utc", now.ToString("O"));
            await user.ExecuteNonQueryAsync(cancellationToken);
        }

        await using (var device = connection.CreateCommand())
        {
            device.Transaction = transaction;
            device.CommandText =
                """
                INSERT INTO devices (
                    device_id, user_id, device_name, platform, approved_utc
                ) VALUES ($deviceId, $userId, $deviceName, $platform, $utc);
                """;
            device.Parameters.AddWithValue("$deviceId", deviceId.ToString("D"));
            device.Parameters.AddWithValue("$userId", userId.ToString("D"));
            device.Parameters.AddWithValue("$deviceName", deviceName);
            device.Parameters.AddWithValue("$platform", platform);
            device.Parameters.AddWithValue("$utc", now.ToString("O"));
            await device.ExecuteNonQueryAsync(cancellationToken);
        }

        await using (var approveRequest = connection.CreateCommand())
        {
            approveRequest.Transaction = transaction;
            approveRequest.CommandText =
                """
                UPDATE join_requests
                SET status = 'APPROVED', decided_by_user_id = $owner, decided_utc = $utc,
                    user_id = $userId, device_id = $deviceId
                WHERE request_id = $requestId AND status = 'PENDING';
                """;
            approveRequest.Parameters.AddWithValue("$owner", ownerUserId.ToString("D"));
            approveRequest.Parameters.AddWithValue("$utc", now.ToString("O"));
            approveRequest.Parameters.AddWithValue("$userId", userId.ToString("D"));
            approveRequest.Parameters.AddWithValue("$deviceId", deviceId.ToString("D"));
            approveRequest.Parameters.AddWithValue("$requestId", requestId.ToString("D"));
            await approveRequest.ExecuteNonQueryAsync(cancellationToken);
        }

        await AppendAuditAsync(connection, transaction, ownerUserId, "JOIN_APPROVED", "DEVICE", deviceId, "{}", now, cancellationToken);
        transaction.Commit();
        return new JoinDecisionResponse(requestId, "APPROVED", userId, deviceId);
    }

    public async Task<SessionResponse> ExchangeApprovedJoinAsync(
        Guid requestId,
        string enrollmentSecret,
        TimeSpan lifetime,
        CancellationToken cancellationToken = default)
    {
        await using var connection = database.OpenConnection();
        using var transaction = connection.BeginTransaction();

        Guid userId;
        Guid deviceId;
        string expectedHash;

        await using (var request = connection.CreateCommand())
        {
            request.Transaction = transaction;
            request.CommandText =
                """
                SELECT user_id, device_id, enrollment_secret_hash, status, enrollment_exchanged_utc
                FROM join_requests
                WHERE request_id = $id;
                """;
            request.Parameters.AddWithValue("$id", requestId.ToString("D"));
            await using var reader = await request.ExecuteReaderAsync(cancellationToken);
            if (!await reader.ReadAsync(cancellationToken))
                throw new KeyNotFoundException("Join request not found.");
            if (reader.GetString(3) != "APPROVED" || !reader.IsDBNull(4))
                throw new UnauthorizedAccessException("Join request is not exchangeable.");

            userId = Guid.Parse(reader.GetString(0));
            deviceId = Guid.Parse(reader.GetString(1));
            expectedHash = reader.GetString(2);
        }

        if (!string.Equals(expectedHash, SecretCodec.HashToken(enrollmentSecret), StringComparison.Ordinal))
            throw new UnauthorizedAccessException("Enrollment secret is invalid.");

        var now = DateTimeOffset.UtcNow;
        await using (var consume = connection.CreateCommand())
        {
            consume.Transaction = transaction;
            consume.CommandText =
                """
                UPDATE join_requests
                SET enrollment_exchanged_utc = $utc
                WHERE request_id = $id AND enrollment_exchanged_utc IS NULL;
                """;
            consume.Parameters.AddWithValue("$utc", now.ToString("O"));
            consume.Parameters.AddWithValue("$id", requestId.ToString("D"));
            if (await consume.ExecuteNonQueryAsync(cancellationToken) != 1)
                throw new UnauthorizedAccessException("Enrollment exchange is already consumed.");
        }

        var session = await CreateSessionAsync(connection, userId, deviceId, "MEMBER", lifetime, cancellationToken, transaction);
        transaction.Commit();
        return session;
    }

    public async Task<SessionPrincipal?> ValidateSessionAsync(
        string bearerToken,
        CancellationToken cancellationToken = default)
    {
        var tokenHash = SecretCodec.HashToken(bearerToken);
        var now = DateTimeOffset.UtcNow;
        await using var connection = database.OpenConnection();
        await using var command = connection.CreateCommand();
        command.CommandText =
            """
            SELECT s.session_id, s.user_id, s.device_id, s.expires_utc, u.role
            FROM device_sessions s
            JOIN users u ON u.user_id = s.user_id
            LEFT JOIN devices d ON d.device_id = s.device_id
            WHERE s.token_hash = $hash
              AND s.revoked_utc IS NULL
              AND s.expires_utc > $now
              AND u.disabled_utc IS NULL
              AND (s.device_id IS NULL OR d.revoked_utc IS NULL);
            """;
        command.Parameters.AddWithValue("$hash", tokenHash);
        command.Parameters.AddWithValue("$now", now.ToString("O"));

        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        if (!await reader.ReadAsync(cancellationToken))
            return null;

        var principal = new SessionPrincipal(
            Guid.Parse(reader.GetString(0)),
            Guid.Parse(reader.GetString(1)),
            reader.IsDBNull(2) ? null : Guid.Parse(reader.GetString(2)),
            reader.GetString(4),
            DateTimeOffset.Parse(reader.GetString(3)));

        await reader.DisposeAsync();
        await using var touch = connection.CreateCommand();
        touch.CommandText = "UPDATE device_sessions SET last_seen_utc = $utc WHERE session_id = $id;";
        touch.Parameters.AddWithValue("$utc", now.ToString("O"));
        touch.Parameters.AddWithValue("$id", principal.SessionId.ToString("D"));
        await touch.ExecuteNonQueryAsync(cancellationToken);
        return principal;
    }

    public async Task RevokeDeviceAsync(
        Guid ownerUserId,
        Guid deviceId,
        CancellationToken cancellationToken = default)
    {
        await using var connection = database.OpenConnection();
        await RequireOwnerAsync(connection, ownerUserId, cancellationToken);
        using var transaction = connection.BeginTransaction();
        var now = DateTimeOffset.UtcNow;

        await using (var device = connection.CreateCommand())
        {
            device.Transaction = transaction;
            device.CommandText =
                "UPDATE devices SET revoked_utc = $utc WHERE device_id = $id AND revoked_utc IS NULL;";
            device.Parameters.AddWithValue("$utc", now.ToString("O"));
            device.Parameters.AddWithValue("$id", deviceId.ToString("D"));
            if (await device.ExecuteNonQueryAsync(cancellationToken) != 1)
                throw new KeyNotFoundException("Active device not found.");
        }

        await using (var sessions = connection.CreateCommand())
        {
            sessions.Transaction = transaction;
            sessions.CommandText =
                "UPDATE device_sessions SET revoked_utc = $utc WHERE device_id = $id AND revoked_utc IS NULL;";
            sessions.Parameters.AddWithValue("$utc", now.ToString("O"));
            sessions.Parameters.AddWithValue("$id", deviceId.ToString("D"));
            await sessions.ExecuteNonQueryAsync(cancellationToken);
        }

        await AppendAuditAsync(connection, transaction, ownerUserId, "DEVICE_REVOKED", "DEVICE", deviceId, "{}", now, cancellationToken);
        transaction.Commit();
    }

    public async Task RevokeSessionAsync(
        Guid ownerUserId,
        Guid sessionId,
        CancellationToken cancellationToken = default)
    {
        await using var connection = database.OpenConnection();
        await RequireOwnerAsync(connection, ownerUserId, cancellationToken);
        using var transaction = connection.BeginTransaction();
        var now = DateTimeOffset.UtcNow;

        await using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText =
            "UPDATE device_sessions SET revoked_utc = $utc WHERE session_id = $id AND revoked_utc IS NULL;";
        command.Parameters.AddWithValue("$utc", now.ToString("O"));
        command.Parameters.AddWithValue("$id", sessionId.ToString("D"));
        if (await command.ExecuteNonQueryAsync(cancellationToken) != 1)
            throw new KeyNotFoundException("Active session not found.");

        await AppendAuditAsync(connection, transaction, ownerUserId, "SESSION_REVOKED", "SESSION", sessionId, "{}", now, cancellationToken);
        transaction.Commit();
    }

    public async Task<bool> IsDiscoveryEnabledAsync(CancellationToken cancellationToken = default)
    {
        await using var connection = database.OpenConnection();
        await using var command = connection.CreateCommand();
        command.CommandText = "SELECT discovery_enabled FROM server_settings WHERE singleton_key = 1;";
        var value = await command.ExecuteScalarAsync(cancellationToken);
        return value is not null && value is not DBNull && Convert.ToInt32(value) == 1;
    }

    private static async Task RequireOwnerAsync(
        SqliteConnection connection,
        Guid ownerUserId,
        CancellationToken cancellationToken)
    {
        await using var command = connection.CreateCommand();
        command.CommandText =
            """
            SELECT COUNT(1)
            FROM users u
            JOIN server_identity s ON s.owner_user_id = u.user_id
            WHERE u.user_id = $id AND u.role = 'OWNER' AND u.disabled_utc IS NULL;
            """;
        command.Parameters.AddWithValue("$id", ownerUserId.ToString("D"));
        if (Convert.ToInt64(await command.ExecuteScalarAsync(cancellationToken)) != 1)
            throw new UnauthorizedAccessException("Configured server owner is required.");
    }

    private static async Task<SessionResponse> CreateSessionAsync(
        SqliteConnection connection,
        Guid userId,
        Guid? deviceId,
        string role,
        TimeSpan lifetime,
        CancellationToken cancellationToken,
        SqliteTransaction? transaction = null)
    {
        if (lifetime <= TimeSpan.Zero || lifetime > TimeSpan.FromDays(30))
            throw new ArgumentOutOfRangeException(nameof(lifetime));

        var sessionId = Guid.NewGuid();
        var token = SecretCodec.NewToken();
        var now = DateTimeOffset.UtcNow;
        var expires = now.Add(lifetime);

        await using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText =
            """
            INSERT INTO device_sessions (
                session_id, user_id, device_id, token_hash, created_utc, expires_utc, last_seen_utc
            ) VALUES (
                $sessionId, $userId, $deviceId, $tokenHash, $created, $expires, $created
            );
            """;
        command.Parameters.AddWithValue("$sessionId", sessionId.ToString("D"));
        command.Parameters.AddWithValue("$userId", userId.ToString("D"));
        command.Parameters.AddWithValue("$deviceId", deviceId is null ? DBNull.Value : deviceId.Value.ToString("D"));
        command.Parameters.AddWithValue("$tokenHash", SecretCodec.HashToken(token));
        command.Parameters.AddWithValue("$created", now.ToString("O"));
        command.Parameters.AddWithValue("$expires", expires.ToString("O"));
        await command.ExecuteNonQueryAsync(cancellationToken);

        return new SessionResponse(sessionId, userId, deviceId, role, token, expires);
    }

    private static async Task AppendAuditAsync(
        SqliteConnection connection,
        SqliteTransaction transaction,
        Guid? actor,
        string eventType,
        string subjectType,
        Guid? subjectId,
        string payloadJson,
        DateTimeOffset createdUtc,
        CancellationToken cancellationToken)
    {
        await using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText =
            """
            INSERT INTO audit_events (
                audit_event_id, actor_user_id, event_type, subject_type, subject_id, payload_json, created_utc
            ) VALUES (
                $id, $actor, $event, $subjectType, $subjectId, $payload, $utc
            );
            """;
        command.Parameters.AddWithValue("$id", Guid.NewGuid().ToString("D"));
        command.Parameters.AddWithValue("$actor", actor is null ? DBNull.Value : actor.Value.ToString("D"));
        command.Parameters.AddWithValue("$event", eventType);
        command.Parameters.AddWithValue("$subjectType", subjectType);
        command.Parameters.AddWithValue("$subjectId", subjectId is null ? DBNull.Value : subjectId.Value.ToString("D"));
        command.Parameters.AddWithValue("$payload", payloadJson);
        command.Parameters.AddWithValue("$utc", createdUtc.ToString("O"));
        await command.ExecuteNonQueryAsync(cancellationToken);
    }
}
