using InterLan.Contracts;
using InterLan.Infrastructure;

var failures = new List<string>();

void Check(bool condition, string name)
{
    Console.WriteLine($"{(condition ? "PASS" : "FAIL")} {name}");
    if (!condition) failures.Add(name);
}

var root = Path.Combine(
    Path.GetTempPath(),
    "interlan-p2-concurrency-" + Guid.NewGuid().ToString("N"));
Directory.CreateDirectory(root);

try
{
    var database = new SqliteDatabase(Path.Combine(root, "p2-concurrency.db"));
    await database.InitializeAsync();

    var alice = Guid.NewGuid();
    var bob = Guid.NewGuid();

    await using (var connection = database.OpenConnection())
    {
        foreach (var user in new[]
        {
            (alice, "concurrent-alice", "Concurrent Alice"),
            (bob, "concurrent-bob", "Concurrent Bob")
        })
        {
            await using var insert = connection.CreateCommand();
            insert.CommandText =
                """
                INSERT INTO users (
                    user_id, username, display_name, role, created_utc
                ) VALUES (
                    $id, $username, $display, 'MEMBER', $utc
                );
                """;
            insert.Parameters.AddWithValue("$id", user.Item1.ToString("D"));
            insert.Parameters.AddWithValue("$username", user.Item2);
            insert.Parameters.AddWithValue("$display", user.Item3);
            insert.Parameters.AddWithValue("$utc", DateTimeOffset.UtcNow.ToString("O"));
            await insert.ExecuteNonQueryAsync();
        }
    }

    var chat = new ChatStore(database);
    var conversation = await chat.GetOrCreateDirectConversationAsync(alice, bob);

    Check(
        conversation.ConversationId != Guid.Empty,
        "concurrency fixture creates canonical direct conversation");

    var uniqueWrites = await Task.WhenAll(
        Enumerable.Range(0, 200)
            .Select(index => chat.SendDirectMessageAsync(
                index % 2 == 0 ? alice : bob,
                conversation.ConversationId,
                new SendMessageRequest(
                    Guid.NewGuid(),
                    $"stress-{index:D3}"))));

    Check(
        uniqueWrites.All(write => write.Created),
        "two hundred concurrent unique writes persist without lock loss");

    var uniqueHistory = await chat.GetDirectHistoryAsync(
        alice,
        conversation.ConversationId,
        null,
        250);

    Check(
        uniqueHistory.Count == 200,
        "concurrent write history contains every unique message");

    var duplicateClientId = Guid.NewGuid();
    var duplicateWrites = await Task.WhenAll(
        Enumerable.Range(0, 64)
            .Select(_ => chat.SendDirectMessageAsync(
                alice,
                conversation.ConversationId,
                new SendMessageRequest(
                    duplicateClientId,
                    "duplicate-race"))));

    Check(
        duplicateWrites.Count(write => write.Created) == 1 &&
        duplicateWrites.Select(write => write.Message.MessageId).Distinct().Count() == 1,
        "sixty-four concurrent duplicate writes collapse to one message");

    var aliceMessages = uniqueHistory
        .Where(message => message.SenderUserId == alice)
        .Select(message => message.MessageId)
        .ToArray();

    await Task.WhenAll(
        aliceMessages.Select(messageId =>
            chat.MarkReadAsync(bob, messageId)));

    Check(
        aliceMessages.Length == 100,
        "one hundred concurrent receipt writes complete without database lock loss");

    var mixedWrites = Task.WhenAll(
        Enumerable.Range(0, 40)
            .Select(index => chat.SendDirectMessageAsync(
                alice,
                conversation.ConversationId,
                new SendMessageRequest(
                    Guid.NewGuid(),
                    $"mixed-{index:D2}"))));

    var mixedReads = Task.WhenAll(
        uniqueHistory
            .Where(message => message.SenderUserId == bob)
            .Take(40)
            .Select(message => chat.MarkReadAsync(
                alice,
                message.MessageId)));

    await Task.WhenAll(mixedWrites, mixedReads);

    Check(
        mixedWrites.Result.All(write => write.Created),
        "mixed message and receipt workloads complete concurrently");
}
finally
{
    try { Directory.Delete(root, recursive: true); } catch { }
}

if (failures.Count > 0)
{
    Console.Error.WriteLine(
        $"P2 concurrency checks failed: {string.Join(", ", failures)}");
    return 1;
}

Console.WriteLine("INTER-LAN P2 CONCURRENCY CHECKS: PASS");
return 0;
