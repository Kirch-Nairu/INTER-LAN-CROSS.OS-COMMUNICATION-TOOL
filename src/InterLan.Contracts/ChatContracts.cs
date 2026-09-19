namespace InterLan.Contracts;

public sealed record UserSummaryResponse(
    Guid UserId,
    string Username,
    string DisplayName,
    string Role);

public sealed record OpenDirectConversationRequest(Guid OtherUserId);

public sealed record DirectConversationResponse(
    Guid ConversationId,
    UserSummaryResponse OtherUser,
    DateTimeOffset CreatedUtc);

public sealed record SendMessageRequest(
    Guid ClientMessageId,
    string Body,
    Guid? ReplyToMessageId = null);

public sealed record MessageResponse(
    Guid MessageId,
    string ScopeType,
    Guid ScopeId,
    Guid SenderUserId,
    Guid ClientMessageId,
    string Body,
    Guid? ReplyToMessageId,
    DateTimeOffset CreatedUtc);

public sealed record PersistedMessageResult(
    MessageResponse Message,
    bool Created);
