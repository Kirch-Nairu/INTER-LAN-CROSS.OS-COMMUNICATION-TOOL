using InterLan.Contracts;
using InterLan.Infrastructure;

var failures = new List<string>();

void Check(bool condition, string name)
{
    if (condition)
    {
        Console.WriteLine($"PASS {name}");
    }
    else
    {
        Console.WriteLine($"FAIL {name}");
        failures.Add(name);
    }
}

async Task<bool> ThrowsAsync<T>(Func<Task> action) where T : Exception
{
    try
    {
        await action();
        return false;
    }
    catch (T)
    {
        return true;
    }
}

var root = Path.Combine(Path.GetTempPath(), "interlan-p1-" + Guid.NewGuid().ToString("N"));
Directory.CreateDirectory(root);

try
{
    var cert1 = ServerCertificateManager.LoadOrCreate(root);
    var cert2 = ServerCertificateManager.LoadOrCreate(root);

    Check(cert1.Sha256Fingerprint == cert2.Sha256Fingerprint, "persistent TLS certificate fingerprint survives reload");
    Check(File.Exists(cert1.PrivateKeyPath) && new FileInfo(cert1.PrivateKeyPath).Length > 0, "server private key persists on owner host");
    Check(cert1.Sha256Fingerprint.Length == 64 && cert1.Sha256Fingerprint.All(Uri.IsHexDigit), "certificate fingerprint is SHA-256 hex");

    var announcement = new DiscoveryAnnouncement(
        LanDiscoveryProtocol.ProtocolId,
        Guid.NewGuid(),
        "P1 Test Server",
        7443,
        cert1.Sha256Fingerprint);

    var encoded = LanDiscoveryProtocol.Serialize(announcement);
    var decoded = LanDiscoveryProtocol.Parse(encoded);
    Check(decoded == announcement, "LAN discovery announcement round-trip");

    var malformedRejected = false;
    try
    {
        LanDiscoveryProtocol.Parse("{\"protocol\":\"evil\"}"u8);
    }
    catch (InvalidDataException)
    {
        malformedRejected = true;
    }
    Check(malformedRejected, "malformed/foreign discovery announcement fails closed");

    var database = new SqliteDatabase(Path.Combine(root, "p1.db"));
    await database.InitializeAsync();
    var store = new EnrollmentStore(database);

    var owner = await store.BootstrapOwnerAsync(
        "P1 Test Server",
        "kirch",
        "Kirch",
        "correct horse battery staple",
        Path.Combine(root, "files"),
        7443,
        true);

    Check(owner.ServerId != Guid.Empty && owner.OwnerUserId != Guid.Empty, "bootstrap creates canonical server and owner");

    var secondBootstrapRejected = await ThrowsAsync<InvalidOperationException>(() =>
        store.BootstrapOwnerAsync(
            "Other",
            "other",
            "Other",
            "another long password",
            Path.Combine(root, "files2"),
            7444,
            false));
    Check(secondBootstrapRejected, "second server-owner bootstrap rejected");

    var ownerSession = await store.LoginOwnerAsync(
        "kirch",
        "correct horse battery staple",
        TimeSpan.FromHours(1));

    Check(ownerSession.Role == "OWNER" && ownerSession.DeviceId is null, "owner login creates owner session");

    var badPasswordRejected = await ThrowsAsync<UnauthorizedAccessException>(() =>
        store.LoginOwnerAsync("kirch", "definitely-wrong", TimeSpan.FromHours(1)));
    Check(badPasswordRejected, "bad owner password rejected");

    var invite = await store.CreateInviteAsync(owner.OwnerUserId, TimeSpan.FromMinutes(30));

    await using (var connection = database.OpenConnection())
    {
        await using var query = connection.CreateCommand();
        query.CommandText = "SELECT token_hash FROM invite_tokens WHERE invite_id = $id;";
        query.Parameters.AddWithValue("$id", invite.InviteId.ToString("D"));
        var persisted = Convert.ToString(await query.ExecuteScalarAsync())!;
        Check(persisted == SecretCodec.HashToken(invite.InviteToken), "invite persisted as deterministic hash");
        Check(!persisted.Contains(invite.InviteToken, StringComparison.Ordinal), "invite plaintext is not persisted");
    }

    var enrollmentSecret = SecretCodec.NewToken();
    var join = await store.SubmitJoinAsync(new SubmitJoinRequest(
        invite.InviteToken,
        "member1",
        "Member One",
        "Android Chrome",
        "android-web",
        enrollmentSecret));

    Check(join.Status == "PENDING", "valid invite creates pending join request");

    var reusedInviteRejected = await ThrowsAsync<UnauthorizedAccessException>(() =>
        store.SubmitJoinAsync(new SubmitJoinRequest(
            invite.InviteToken,
            "member2",
            "Member Two",
            "Laptop",
            "windows",
            SecretCodec.NewToken())));
    Check(reusedInviteRejected, "consumed invite cannot be replayed");

    var decision = await store.DecideJoinAsync(owner.OwnerUserId, join.RequestId, approve: true);
    Check(decision.Status == "APPROVED" && decision.DeviceId is not null && decision.UserId is not null, "owner approval creates member and device");

    var memberSession = await store.ExchangeApprovedJoinAsync(
        join.RequestId,
        enrollmentSecret,
        TimeSpan.FromHours(2));

    Check(memberSession.Role == "MEMBER" && memberSession.DeviceId == decision.DeviceId, "approved device exchanges enrollment secret for member session");

    var secondExchangeRejected = await ThrowsAsync<UnauthorizedAccessException>(() =>
        store.ExchangeApprovedJoinAsync(join.RequestId, enrollmentSecret, TimeSpan.FromHours(2)));
    Check(secondExchangeRejected, "enrollment exchange is one-use");

    await using (var connection = database.OpenConnection())
    {
        await using var query = connection.CreateCommand();
        query.CommandText = "SELECT token_hash FROM device_sessions WHERE session_id = $id;";
        query.Parameters.AddWithValue("$id", memberSession.SessionId.ToString("D"));
        var persisted = Convert.ToString(await query.ExecuteScalarAsync())!;
        Check(persisted == SecretCodec.HashToken(memberSession.BearerToken), "session persisted as hash");
        Check(!persisted.Contains(memberSession.BearerToken, StringComparison.Ordinal), "session plaintext is not persisted");
    }

    var principal = await store.ValidateSessionAsync(memberSession.BearerToken);
    Check(principal is not null && principal.Role == "MEMBER", "active member session validates");

    await store.RevokeDeviceAsync(owner.OwnerUserId, decision.DeviceId!.Value);
    var revoked = await store.ValidateSessionAsync(memberSession.BearerToken);
    Check(revoked is null, "device revocation invalidates existing sessions");

    var ownerPrincipal = await store.ValidateSessionAsync(ownerSession.BearerToken);
    Check(ownerPrincipal is not null && ownerPrincipal.Role == "OWNER", "owner session remains valid after member revocation");

    Check(await store.IsDiscoveryEnabledAsync(), "persisted discovery policy is enabled");

    await store.RevokeSessionAsync(owner.OwnerUserId, ownerSession.SessionId);
    var revokedOwner = await store.ValidateSessionAsync(ownerSession.BearerToken);
    Check(revokedOwner is null, "explicit owner-authorized session revocation invalidates token");

    await using (var connection = database.OpenConnection())
    {
        await using var audit = connection.CreateCommand();
        audit.CommandText =
            """
            SELECT COUNT(1)
            FROM audit_events
            WHERE event_type IN (
                'SERVER_BOOTSTRAPPED',
                'INVITE_CREATED',
                'JOIN_REQUESTED',
                'JOIN_APPROVED',
                'SESSION_CREATED',
                'DEVICE_REVOKED',
                'SESSION_REVOKED'
            );
            """;
        var auditCount = Convert.ToInt64(await audit.ExecuteScalarAsync());
        Check(auditCount >= 7, "identity/enrollment security events are auditable");
    }
}
finally
{
    try
    {
        Directory.Delete(root, recursive: true);
    }
    catch
    {
        // CI cleanup is best effort.
    }
}

if (failures.Count > 0)
{
    Console.Error.WriteLine($"P1 checks failed: {string.Join(", ", failures)}");
    return 1;
}

Console.WriteLine("INTER-LAN P1 LAN/IDENTITY CHECKS: PASS");
return 0;
