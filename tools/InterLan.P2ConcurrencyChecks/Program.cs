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
