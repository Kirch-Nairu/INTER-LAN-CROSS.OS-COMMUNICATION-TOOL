using InterLan.Server;
using Microsoft.AspNetCore.Http;
using System.Net;
using InterLan.Contracts;
using InterLan.Infrastructure;

var rateContextA = new DefaultHttpContext();
rateContextA.Connection.RemoteIpAddress = IPAddress.Parse("192.168.10.20");
var rateContextB = new DefaultHttpContext();
rateContextB.Connection.RemoteIpAddress = IPAddress.Parse("192.168.10.21");

Check(
    RateLimitPartitionKeys.RemoteAddress(rateContextA) !=
    RateLimitPartitionKeys.RemoteAddress(rateContextB),
    "rate-limit IP partitions isolate distinct clients");

var authRateContextA = new DefaultHttpContext();
authRateContextA.Request.Headers.Authorization = "Bearer p2-rate-limit-token-a";
var authRateContextAReplay = new DefaultHttpContext();
authRateContextAReplay.Request.Headers.Authorization = "Bearer p2-rate-limit-token-a";

var authPartition = RateLimitPartitionKeys.AuthenticatedClient(authRateContextA);
Check(
    authPartition == RateLimitPartitionKeys.AuthenticatedClient(authRateContextAReplay) &&
    !authPartition.Contains("p2-rate-limit-token-a", StringComparison.Ordinal),
    "authenticated rate-limit partition is stable without exposing bearer token");

var failures = new List<string>();

void Check(bool condition, string name)
{
    Console.WriteLine($"{(condition ? "PASS" : "FAIL")} {name}");
    if (!condition) failures.Add(name);
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

var root = Path.Combine(Path.GetTempPath(), "interlan-p2-" + Guid.NewGuid().ToString("N"));
Directory.CreateDirectory(root);

try
{
    var database = new SqliteDatabase(Path.Combine(root, "p2.db"));
    await database.InitializeAsync();

    await using (var connection = database.OpenConnection())
    {
        await using var journal = connection.CreateCommand();
        journal.CommandText = "PRAGMA journal_mode;";
        var journalMode = Convert.ToString(await journal.ExecuteScalarAsync());
        Check(string.Equals(journalMode, "wal", StringComparison.OrdinalIgnoreCase),
            "SQLite uses WAL journal mode");

        await using var timeout = connection.CreateCommand();
        timeout.CommandText = "PRAGMA busy_timeout;";
        var busyTimeout = Convert.ToInt32(await timeout.ExecuteScalarAsync());
        Check(busyTimeout >= 5000, "SQLite connection enforces busy timeout");
    }

    var alice = Guid.NewGuid();
    var bob = Guid.NewGuid();
    var carol = Guid.NewGuid();

    await using (var connection = database.OpenConnection())
    {
        foreach (var user in new[]
        {
            (alice, "alice", "Alice"),
            (bob, "bob", "Bob"),
            (carol, "carol", "Carol")
        })
        {
            await using var command = connection.CreateCommand();
            command.CommandText =
                """
                INSERT INTO users (user_id, username, display_name, role, created_utc)
                VALUES ($id, $username, $display, 'MEMBER', $utc);
                """;
            command.Parameters.AddWithValue("$id", user.Item1.ToString("D"));
            command.Parameters.AddWithValue("$username", user.Item2);
            command.Parameters.AddWithValue("$display", user.Item3);
            command.Parameters.AddWithValue("$utc", DateTimeOffset.UtcNow.ToString("O"));
            await command.ExecuteNonQueryAsync();
        }
    }

    var chat = new ChatStore(database);

    var users = await chat.ListUsersAsync(alice);
    Check(users.Count == 3, "authenticated user directory returns active users");

    var ab1 = await chat.GetOrCreateDirectConversationAsync(alice, bob);
    var ab2 = await chat.GetOrCreateDirectConversationAsync(bob, alice);
    Check(ab1.ConversationId == ab2.ConversationId, "same user pair resolves to one canonical direct conversation");

    var concurrentConversationIds = await Task.WhenAll(
        Enumerable.Range(0, 16)
            .Select(index => index % 2 == 0
                ? chat.GetOrCreateDirectConversationAsync(alice, bob)
                : chat.GetOrCreateDirectConversationAsync(bob, alice)));

    Check(
        concurrentConversationIds.All(conversation => conversation.ConversationId == ab1.ConversationId),
        "concurrent direct-conversation creation resolves to one canonical conversation");

    var ac = await chat.GetOrCreateDirectConversationAsync(alice, carol);
    Check(ac.ConversationId != ab1.ConversationId, "different pair gets different conversation");

    var listed = await chat.ListDirectConversationsAsync(alice);
    Check(listed.Count == 2, "direct conversation listing is membership scoped");

    var client1 = Guid.NewGuid();
    var first = await chat.SendDirectMessageAsync(
        alice,
        ab1.ConversationId,
        new SendMessageRequest(client1, "hello bob"));

    var duplicate = await chat.SendDirectMessageAsync(
        alice,
        ab1.ConversationId,
        new SendMessageRequest(client1, "hello bob"));

    Check(first.Created && !duplicate.Created && first.Message.MessageId == duplicate.Message.MessageId,
        "duplicate client message ID is idempotent");

    await Task.Delay(2);

    var second = await chat.SendDirectMessageAsync(
        bob,
        ab1.ConversationId,
        new SendMessageRequest(Guid.NewGuid(), "hello alice"));

    var history = await chat.GetDirectHistoryAsync(alice, ab1.ConversationId, null, 100);
    Check(history.Count == 2 && history[0].MessageId == first.Message.MessageId &&
          history[1].MessageId == second.Message.MessageId,
        "direct history is ordered and durable");

    var catchup = await chat.GetDirectHistoryAsync(alice, ab1.ConversationId, first.Message.MessageId, 100);
    Check(catchup.Count == 1 && catchup[0].MessageId == second.Message.MessageId,
        "message cursor returns reconnect catch-up without duplicates");

    Check(await ThrowsAsync<UnauthorizedAccessException>(() =>
        chat.GetDirectHistoryAsync(carol, ab1.ConversationId, null, 100)),
        "non-member cannot read another direct conversation");

    Check(await ThrowsAsync<UnauthorizedAccessException>(() =>
        chat.SendDirectMessageAsync(
            carol,
            ab1.ConversationId,
            new SendMessageRequest(Guid.NewGuid(), "intrusion"))),
        "non-member cannot send into another direct conversation");

    Check(await ThrowsAsync<ArgumentException>(() =>
        chat.SendDirectMessageAsync(
            alice,
            ab1.ConversationId,
            new SendMessageRequest(Guid.NewGuid(), new string('x', ChatStore.MaxMessageLength + 1)))),
        "oversized message fails closed");

    Check(await ThrowsAsync<InvalidOperationException>(() =>
        chat.MarkDeliveredAsync(alice, first.Message.MessageId)),
        "sender cannot acknowledge own message as delivered");

    Check(await ThrowsAsync<InvalidOperationException>(() =>
        chat.MarkReadAsync(alice, first.Message.MessageId)),
        "sender cannot acknowledge own message as read");

    await chat.MarkDeliveredAsync(bob, first.Message.MessageId);
    await chat.MarkDeliveredAsync(bob, first.Message.MessageId);

    var deliveredReceipts = await chat.GetReceiptsAsync(alice, first.Message.MessageId);
    Check(
        deliveredReceipts.Count == 1 &&
        deliveredReceipts[0].UserId == bob &&
        deliveredReceipts[0].DeliveredUtc is not null &&
        deliveredReceipts[0].ReadUtc is null,
        "recipient delivery acknowledgement is durable and idempotent");

    await chat.MarkReadAsync(bob, first.Message.MessageId);
    await chat.MarkReadAsync(bob, first.Message.MessageId);

    var readReceipts = await chat.GetReceiptsAsync(alice, first.Message.MessageId);
    Check(
        readReceipts.Count == 1 &&
        readReceipts[0].UserId == bob &&
        readReceipts[0].DeliveredUtc is not null &&
        readReceipts[0].ReadUtc is not null,
        "recipient read acknowledgement advances receipt state idempotently");

    var concurrentSends = await Task.WhenAll(
        Enumerable.Range(0, 20)
            .Select(index => chat.SendDirectMessageAsync(
                alice,
                ac.ConversationId,
                new SendMessageRequest(Guid.NewGuid(), $"concurrent-{index:D2}"))));

    Check(
        concurrentSends.Count(result => result.Created) == 20,
        "concurrent unique sends all persist exactly once");

    var concurrentHistory = await chat.GetDirectHistoryAsync(
        carol,
        ac.ConversationId,
        null,
        100);

    Check(
        concurrentHistory.Count == 20,
        "concurrent unique sends remain fully readable");

    var duplicateClientMessageId = Guid.NewGuid();
    var duplicateRace = await Task.WhenAll(
        Enumerable.Range(0, 12)
            .Select(_ => chat.SendDirectMessageAsync(
                alice,
                ac.ConversationId,
                new SendMessageRequest(duplicateClientMessageId, "same-idempotent-payload"))));

    Check(
        duplicateRace.Count(result => result.Created) == 1 &&
        duplicateRace.Select(result => result.Message.MessageId).Distinct().Count() == 1,
        "concurrent duplicate client message IDs collapse to one durable message");

    Check(await ThrowsAsync<InvalidOperationException>(() =>
        chat.SendDirectMessageAsync(
            alice,
            ac.ConversationId,
            new SendMessageRequest(
                duplicateClientMessageId,
                "different-payload-for-same-idempotency-key"))),
        "idempotency key replay with different content fails closed");

    var afterDuplicateRace = await chat.GetDirectHistoryAsync(
        carol,
        ac.ConversationId,
        null,
        100);

    Check(
        afterDuplicateRace.Count == 21,
        "concurrent duplicate race adds exactly one message");

    var reopened = new SqliteDatabase(Path.Combine(root, "p2.db"));
    await reopened.InitializeAsync();
    var reopenedChat = new ChatStore(reopened);
    var afterRestart = await reopenedChat.GetDirectHistoryAsync(alice, ab1.ConversationId, null, 100);
    Check(afterRestart.Count == 2, "message history survives database restart");
}
finally
{
    try { Directory.Delete(root, true); } catch { }
}

if (failures.Count > 0)
{
    Console.Error.WriteLine($"P2 checks failed: {string.Join(", ", failures)}");
    return 1;
}

Console.WriteLine("INTER-LAN P2 DIRECT MESSAGING CHECKS: PASS");
return 0;
