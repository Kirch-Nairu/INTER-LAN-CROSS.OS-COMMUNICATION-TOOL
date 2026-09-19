using InterLan.Contracts;
using Microsoft.Data.Sqlite;

namespace InterLan.Infrastructure;

public sealed class ChatStore(SqliteDatabase database)
{
    public const int MaxMessageLength = 4_000;

    public async Task<IReadOnlyList<UserSummaryResponse>> ListUsersAsync(
        Guid actorUserId,
        CancellationToken cancellationToken = default)
    {
        await using var connection = database.OpenConnection();
        await RequireActiveUserAsync(connection, actorUserId, cancellationToken);

        await using var command = connection.CreateCommand();
        command.CommandText =
            """
            SELECT user_id, username, display_name, role
            FROM users
            WHERE disabled_utc IS NULL
            ORDER BY lower(display_name), lower(username), user_id;
            """;

        var users = new List<UserSummaryResponse>();
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            users.Add(new UserSummaryResponse(
                Guid.Parse(reader.GetString(0)),
                reader.GetString(1),
                reader.GetString(2),
                reader.GetString(3)));
        }

        return users;
    }

    public async Task<DirectConversationResponse> GetOrCreateDirectConversationAsync(
        Guid actorUserId,
        Guid otherUserId,
        CancellationToken cancellationToken = default)
    {
        if (actorUserId == Guid.Empty || otherUserId == Guid.Empty)
            throw new ArgumentException("User IDs must be non-empty.");
        if (actorUserId == otherUserId)
            throw new ArgumentException("A direct conversation requires two distinct users.");

        await using var connection = database.OpenConnection();
        await RequireActiveUserAsync(connection, actorUserId, cancellationToken);
        var other = await GetActiveUserAsync(connection, otherUserId, cancellationToken)
            ?? throw new KeyNotFoundException("The requested user does not exist or is disabled.");

        var pairKey = PairKey(actorUserId, otherUserId);
        var candidateId = Guid.NewGuid();
        var now = DateTimeOffset.UtcNow;

        using var transaction = connection.BeginTransaction();

        await using (var conversation = connection.CreateCommand())
        {
            conversation.Transaction = transaction;
            conversation.CommandText =
                """
                INSERT OR IGNORE INTO direct_conversations (
                    conversation_id, created_utc, pair_key
                ) VALUES ($id, $created, $pairKey);
                """;
            conversation.Parameters.AddWithValue("$id", candidateId.ToString("D"));
            conversation.Parameters.AddWithValue("$created", now.ToString("O"));
            conversation.Parameters.AddWithValue("$pairKey", pairKey);
            await conversation.ExecuteNonQueryAsync(cancellationToken);
        }

        Guid conversationId;
        DateTimeOffset createdUtc;
        await using (var resolve = connection.CreateCommand())
        {
            resolve.Transaction = transaction;
            resolve.CommandText =
                """
                SELECT conversation_id, created_utc
                FROM direct_conversations
                WHERE pair_key = $pairKey;
                """;
            resolve.Parameters.AddWithValue("$pairKey", pairKey);
            await using var reader = await resolve.ExecuteReaderAsync(cancellationToken);
            if (!await reader.ReadAsync(cancellationToken))
                throw new InvalidOperationException("Direct conversation could not be resolved.");

            conversationId = Guid.Parse(reader.GetString(0));
            createdUtc = DateTimeOffset.Parse(reader.GetString(1));
        }

        foreach (var memberId in new[] { actorUserId, otherUserId })
        {
            await using var member = connection.CreateCommand();
            member.Transaction = transaction;
            member.CommandText =
                """
                INSERT OR IGNORE INTO direct_conversation_members (
                    conversation_id, user_id, joined_utc
                ) VALUES ($conversationId, $userId, $joined);
                """;
            member.Parameters.AddWithValue("$conversationId", conversationId.ToString("D"));
            member.Parameters.AddWithValue("$userId", memberId.ToString("D"));
            member.Parameters.AddWithValue("$joined", now.ToString("O"));
            await member.ExecuteNonQueryAsync(cancellationToken);
        }

        transaction.Commit();
        return new DirectConversationResponse(conversationId, other, createdUtc);
    }

    public async Task<IReadOnlyList<DirectConversationResponse>> ListDirectConversationsAsync(
        Guid actorUserId,
        CancellationToken cancellationToken = default)
    {
        await using var connection = database.OpenConnection();
        await RequireActiveUserAsync(connection, actorUserId, cancellationToken);

        await using var command = connection.CreateCommand();
        command.CommandText =
            """
            SELECT c.conversation_id, c.created_utc,
                   u.user_id, u.username, u.display_name, u.role
            FROM direct_conversations c
            JOIN direct_conversation_members mine
              ON mine.conversation_id = c.conversation_id
             AND mine.user_id = $actor
            JOIN direct_conversation_members other
              ON other.conversation_id = c.conversation_id
             AND other.user_id <> $actor
            JOIN users u
              ON u.user_id = other.user_id
             AND u.disabled_utc IS NULL
            ORDER BY c.created_utc DESC, c.conversation_id DESC;
            """;
        command.Parameters.AddWithValue("$actor", actorUserId.ToString("D"));

        var conversations = new List<DirectConversationResponse>();
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            conversations.Add(new DirectConversationResponse(
                Guid.Parse(reader.GetString(0)),
                new UserSummaryResponse(
                    Guid.Parse(reader.GetString(2)),
                    reader.GetString(3),
                    reader.GetString(4),
                    reader.GetString(5)),
                DateTimeOffset.Parse(reader.GetString(1))));
        }

        return conversations;
    }

    public async Task<PersistedMessageResult> SendDirectMessageAsync(
        Guid actorUserId,
        Guid conversationId,
        SendMessageRequest request,
        CancellationToken cancellationToken = default)
    {
        if (request.ClientMessageId == Guid.Empty)
            throw new ArgumentException("ClientMessageId must be non-empty.");
        if (string.IsNullOrWhiteSpace(request.Body))
            throw new ArgumentException("Message body is required.");
        if (request.Body.Length > MaxMessageLength)
            throw new ArgumentException($"Message body cannot exceed {MaxMessageLength} characters.");

        var body = request.Body.Trim();
        await using var connection = database.OpenConnection();
        await RequireDirectMembershipAsync(connection, actorUserId, conversationId, cancellationToken);

        using var transaction = connection.BeginTransaction();

        var existing = await FindByClientMessageIdAsync(
            connection,
            transaction,
            actorUserId,
            request.ClientMessageId,
            cancellationToken);

        if (existing is not null)
        {
            if (existing.ScopeType != "DIRECT" || existing.ScopeId != conversationId)
                throw new InvalidOperationException("ClientMessageId was already used for another message.");

            transaction.Commit();
            return new PersistedMessageResult(existing, false);
        }

        if (request.ReplyToMessageId is { } replyTo)
        {
            await using var reply = connection.CreateCommand();
            reply.Transaction = transaction;
            reply.CommandText =
                """
                SELECT COUNT(1)
                FROM messages
                WHERE message_id = $messageId
                  AND scope_type = 'DIRECT'
                  AND scope_id = $conversationId
                  AND deleted_utc IS NULL;
                """;
            reply.Parameters.AddWithValue("$messageId", replyTo.ToString("D"));
            reply.Parameters.AddWithValue("$conversationId", conversationId.ToString("D"));
            if (Convert.ToInt64(await reply.ExecuteScalarAsync(cancellationToken)) != 1)
                throw new ArgumentException("Reply target is not an active message in this conversation.");
        }

        var messageId = Guid.NewGuid();
        var now = DateTimeOffset.UtcNow;

        await using (var insert = connection.CreateCommand())
        {
            insert.Transaction = transaction;
            insert.CommandText =
                """
                INSERT INTO messages (
                    message_id, scope_type, scope_id, sender_user_id,
                    client_message_id, body, reply_to_message_id, created_utc
                ) VALUES (
                    $messageId, 'DIRECT', $scopeId, $sender,
                    $clientMessageId, $body, $replyTo, $created
                );
                """;
            insert.Parameters.AddWithValue("$messageId", messageId.ToString("D"));
            insert.Parameters.AddWithValue("$scopeId", conversationId.ToString("D"));
            insert.Parameters.AddWithValue("$sender", actorUserId.ToString("D"));
            insert.Parameters.AddWithValue("$clientMessageId", request.ClientMessageId.ToString("D"));
            insert.Parameters.AddWithValue("$body", body);
            insert.Parameters.AddWithValue("$replyTo", request.ReplyToMessageId is null
                ? DBNull.Value
                : request.ReplyToMessageId.Value.ToString("D"));
            insert.Parameters.AddWithValue("$created", now.ToString("O"));
            await insert.ExecuteNonQueryAsync(cancellationToken);
        }

        transaction.Commit();

        return new PersistedMessageResult(
            new MessageResponse(
                messageId,
                "DIRECT",
                conversationId,
                actorUserId,
                request.ClientMessageId,
                body,
                request.ReplyToMessageId,
                now),
            true);
    }

    public async Task<IReadOnlyList<MessageResponse>> GetDirectHistoryAsync(
        Guid actorUserId,
        Guid conversationId,
        Guid? afterMessageId,
        int limit,
        CancellationToken cancellationToken = default)
    {
        if (limit is < 1 or > 250)
            throw new ArgumentOutOfRangeException(nameof(limit));

        await using var connection = database.OpenConnection();
        await RequireDirectMembershipAsync(connection, actorUserId, conversationId, cancellationToken);

        string? afterCreated = null;
        string? afterId = null;
        if (afterMessageId is { } cursor)
        {
            await using var cursorCommand = connection.CreateCommand();
            cursorCommand.CommandText =
                """
                SELECT created_utc, message_id
                FROM messages
                WHERE message_id = $messageId
                  AND scope_type = 'DIRECT'
                  AND scope_id = $conversationId;
                """;
            cursorCommand.Parameters.AddWithValue("$messageId", cursor.ToString("D"));
            cursorCommand.Parameters.AddWithValue("$conversationId", conversationId.ToString("D"));
            await using var reader = await cursorCommand.ExecuteReaderAsync(cancellationToken);
            if (!await reader.ReadAsync(cancellationToken))
                throw new KeyNotFoundException("Message cursor was not found in this conversation.");

            afterCreated = reader.GetString(0);
            afterId = reader.GetString(1);
        }

        await using var command = connection.CreateCommand();
        command.CommandText =
            """
            SELECT message_id, scope_type, scope_id, sender_user_id,
                   client_message_id, body, reply_to_message_id, created_utc
            FROM messages
            WHERE scope_type = 'DIRECT'
              AND scope_id = $conversationId
              AND deleted_utc IS NULL
              AND (
                  $afterCreated IS NULL
                  OR created_utc > $afterCreated
                  OR (created_utc = $afterCreated AND message_id > $afterId)
              )
            ORDER BY created_utc, message_id
            LIMIT $limit;
            """;
        command.Parameters.AddWithValue("$conversationId", conversationId.ToString("D"));
        command.Parameters.AddWithValue("$afterCreated", afterCreated is null ? DBNull.Value : afterCreated);
        command.Parameters.AddWithValue("$afterId", afterId is null ? DBNull.Value : afterId);
        command.Parameters.AddWithValue("$limit", limit);

        var messages = new List<MessageResponse>();
        await using var history = await command.ExecuteReaderAsync(cancellationToken);
        while (await history.ReadAsync(cancellationToken))
        {
            messages.Add(ReadMessage(history));
        }

        return messages;
    }

    public async Task<IReadOnlyList<Guid>> GetDirectMemberIdsAsync(
        Guid actorUserId,
        Guid conversationId,
        CancellationToken cancellationToken = default)
    {
        await using var connection = database.OpenConnection();
        await RequireDirectMembershipAsync(connection, actorUserId, conversationId, cancellationToken);

        await using var command = connection.CreateCommand();
        command.CommandText =
            """
            SELECT user_id
            FROM direct_conversation_members
            WHERE conversation_id = $conversationId
            ORDER BY user_id;
            """;
        command.Parameters.AddWithValue("$conversationId", conversationId.ToString("D"));

        var members = new List<Guid>(2);
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
            members.Add(Guid.Parse(reader.GetString(0)));

        return members;
    }

    public async Task MarkDeliveredAsync(
        Guid actorUserId,
        Guid messageId,
        CancellationToken cancellationToken = default)
    {
        await using var connection = database.OpenConnection();
        var target = await GetDirectReceiptTargetAsync(connection, messageId, cancellationToken);

        await RequireDirectMembershipAsync(connection, actorUserId, target.ConversationId, cancellationToken);
        if (target.SenderUserId == actorUserId)
            throw new InvalidOperationException("A sender cannot acknowledge their own message as delivered.");

        var now = DateTimeOffset.UtcNow.ToString("O");

        await using var receipt = connection.CreateCommand();
        receipt.CommandText =
            """
            INSERT INTO message_receipts (
                message_id, user_id, delivered_utc, read_utc
            ) VALUES ($messageId, $userId, $utc, NULL)
            ON CONFLICT(message_id, user_id) DO UPDATE SET
                delivered_utc = COALESCE(message_receipts.delivered_utc, excluded.delivered_utc);
            """;
        receipt.Parameters.AddWithValue("$messageId", messageId.ToString("D"));
        receipt.Parameters.AddWithValue("$userId", actorUserId.ToString("D"));
        receipt.Parameters.AddWithValue("$utc", now);
        await receipt.ExecuteNonQueryAsync(cancellationToken);
    }

    public async Task MarkReadAsync(
        Guid actorUserId,
        Guid messageId,
        CancellationToken cancellationToken = default)
    {
        await using var connection = database.OpenConnection();
        var target = await GetDirectReceiptTargetAsync(connection, messageId, cancellationToken);

        await RequireDirectMembershipAsync(connection, actorUserId, target.ConversationId, cancellationToken);
        if (target.SenderUserId == actorUserId)
            throw new InvalidOperationException("A sender cannot acknowledge their own message as read.");

        var now = DateTimeOffset.UtcNow.ToString("O");

        await using var receipt = connection.CreateCommand();
        receipt.CommandText =
            """
            INSERT INTO message_receipts (
                message_id, user_id, delivered_utc, read_utc
            ) VALUES ($messageId, $userId, $utc, $utc)
            ON CONFLICT(message_id, user_id) DO UPDATE SET
                delivered_utc = COALESCE(message_receipts.delivered_utc, excluded.delivered_utc),
                read_utc = COALESCE(message_receipts.read_utc, excluded.read_utc);
            """;
        receipt.Parameters.AddWithValue("$messageId", messageId.ToString("D"));
        receipt.Parameters.AddWithValue("$userId", actorUserId.ToString("D"));
        receipt.Parameters.AddWithValue("$utc", now);
        await receipt.ExecuteNonQueryAsync(cancellationToken);
    }

    private static async Task<(Guid ConversationId, Guid SenderUserId)> GetDirectReceiptTargetAsync(
        SqliteConnection connection,
        Guid messageId,
        CancellationToken cancellationToken)
    {
        await using var message = connection.CreateCommand();
        message.CommandText =
            """
            SELECT scope_id, sender_user_id
            FROM messages
            WHERE message_id = $messageId
              AND scope_type = 'DIRECT'
              AND deleted_utc IS NULL;
            """;
        message.Parameters.AddWithValue("$messageId", messageId.ToString("D"));

        await using var reader = await message.ExecuteReaderAsync(cancellationToken);
        if (!await reader.ReadAsync(cancellationToken))
            throw new KeyNotFoundException("Message not found.");

        return (
            Guid.Parse(reader.GetString(0)),
            Guid.Parse(reader.GetString(1)));
    }

    private static string PairKey(Guid left, Guid right)
    {
        var a = left.ToString("D");
        var b = right.ToString("D");
        return string.CompareOrdinal(a, b) < 0 ? $"{a}:{b}" : $"{b}:{a}";
    }

    private static async Task RequireActiveUserAsync(
        SqliteConnection connection,
        Guid userId,
        CancellationToken cancellationToken)
    {
        if (await GetActiveUserAsync(connection, userId, cancellationToken) is null)
            throw new UnauthorizedAccessException("Active user is required.");
    }

    private static async Task<UserSummaryResponse?> GetActiveUserAsync(
        SqliteConnection connection,
        Guid userId,
        CancellationToken cancellationToken)
    {
        await using var command = connection.CreateCommand();
        command.CommandText =
            """
            SELECT user_id, username, display_name, role
            FROM users
            WHERE user_id = $userId
              AND disabled_utc IS NULL;
            """;
        command.Parameters.AddWithValue("$userId", userId.ToString("D"));
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        if (!await reader.ReadAsync(cancellationToken))
            return null;

        return new UserSummaryResponse(
            Guid.Parse(reader.GetString(0)),
            reader.GetString(1),
            reader.GetString(2),
            reader.GetString(3));
    }

    private static async Task RequireDirectMembershipAsync(
        SqliteConnection connection,
        Guid userId,
        Guid conversationId,
        CancellationToken cancellationToken)
    {
        await using var command = connection.CreateCommand();
        command.CommandText =
            """
            SELECT COUNT(1)
            FROM direct_conversation_members m
            JOIN users u ON u.user_id = m.user_id
            WHERE m.conversation_id = $conversationId
              AND m.user_id = $userId
              AND u.disabled_utc IS NULL;
            """;
        command.Parameters.AddWithValue("$conversationId", conversationId.ToString("D"));
        command.Parameters.AddWithValue("$userId", userId.ToString("D"));
        if (Convert.ToInt64(await command.ExecuteScalarAsync(cancellationToken)) != 1)
            throw new UnauthorizedAccessException("Direct conversation membership is required.");
    }

    private static async Task<MessageResponse?> FindByClientMessageIdAsync(
        SqliteConnection connection,
        SqliteTransaction transaction,
        Guid senderUserId,
        Guid clientMessageId,
        CancellationToken cancellationToken)
    {
        await using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText =
            """
            SELECT message_id, scope_type, scope_id, sender_user_id,
                   client_message_id, body, reply_to_message_id, created_utc
            FROM messages
            WHERE sender_user_id = $sender
              AND client_message_id = $clientMessageId;
            """;
        command.Parameters.AddWithValue("$sender", senderUserId.ToString("D"));
        command.Parameters.AddWithValue("$clientMessageId", clientMessageId.ToString("D"));

        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        if (!await reader.ReadAsync(cancellationToken))
            return null;

        return ReadMessage(reader);
    }

    private static MessageResponse ReadMessage(SqliteDataReader reader) =>
        new(
            Guid.Parse(reader.GetString(0)),
            reader.GetString(1),
            Guid.Parse(reader.GetString(2)),
            Guid.Parse(reader.GetString(3)),
            Guid.Parse(reader.GetString(4)),
            reader.IsDBNull(5) ? string.Empty : reader.GetString(5),
            reader.IsDBNull(6) ? null : Guid.Parse(reader.GetString(6)),
            DateTimeOffset.Parse(reader.GetString(7)));
}
