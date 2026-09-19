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
    DateTimeOffset CreatedUtc,
    DateTimeOffset? EditedUtc = null,
    DateTimeOffset? DeletedUtc = null);

public sealed record PersistedMessageResult(
    MessageResponse Message,
    bool Created);


public sealed record MessageReceiptResponse(
    Guid MessageId,
    Guid UserId,
    DateTimeOffset? DeliveredUtc,
    DateTimeOffset? ReadUtc);

public sealed record DirectConversationSummaryResponse(
    Guid ConversationId,
    UserSummaryResponse OtherUser,
    DateTimeOffset CreatedUtc,
    MessageResponse? LastMessage,
    int UnreadCount);

public sealed record MessagePageResponse(
    IReadOnlyList<MessageResponse> Items,
    Guid? NextAfterMessageId,
    bool HasMore);

public sealed record RealtimeTicketResponse(
    string Ticket,
    DateTimeOffset ExpiresUtc);

public sealed record EditMessageRequest(string Body);

public sealed record MessageDeletedResponse(
    Guid MessageId,
    Guid ConversationId,
    DateTimeOffset DeletedUtc);

public sealed record MarkConversationReadRequest(
    Guid? UpToMessageId = null);

public sealed record ConversationReadResponse(
    Guid ConversationId,
    int MarkedCount,
    DateTimeOffset ReadUtc);

public sealed record MessageReceiptsChangedResponse(
    Guid ConversationId,
    Guid MessageId,
    IReadOnlyList<MessageReceiptResponse> Receipts);
