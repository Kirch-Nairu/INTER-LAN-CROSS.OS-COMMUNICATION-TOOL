using System.Net.Http.Headers;
using System.Net.Http.Json;
using InterLan.Contracts;

namespace InterLan.Application;

public sealed class InterLanApiClient(HttpClient httpClient)
{
    private readonly HttpClient _httpClient = httpClient
        ?? throw new ArgumentNullException(nameof(httpClient));

    public HttpClient HttpClient => _httpClient;

    public void SetBearerToken(string bearerToken)
    {
        if (string.IsNullOrWhiteSpace(bearerToken))
            throw new ArgumentException("Bearer token is required.", nameof(bearerToken));

        _httpClient.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", bearerToken);
    }

    public async Task<IReadOnlyList<UserSummaryResponse>> ListUsersAsync(
        CancellationToken cancellationToken = default)
    {
        RequireAuthenticated();

        return await _httpClient.GetFromJsonAsync<UserSummaryResponse[]>(
            "/api/v1/users",
            cancellationToken)
            ?? Array.Empty<UserSummaryResponse>();
    }

    public async Task<DirectConversationResponse> OpenDirectConversationAsync(
        Guid otherUserId,
        CancellationToken cancellationToken = default)
    {
        RequireAuthenticated();

        using var response = await _httpClient.PostAsJsonAsync(
            "/api/v1/direct",
            new OpenDirectConversationRequest(otherUserId),
            cancellationToken);

        response.EnsureSuccessStatusCode();

        return await response.Content.ReadFromJsonAsync<DirectConversationResponse>(
            cancellationToken: cancellationToken)
            ?? throw new InvalidDataException("Direct conversation response was empty.");
    }

    public async Task<IReadOnlyList<DirectConversationSummaryResponse>> ListDirectConversationSummariesAsync(
        CancellationToken cancellationToken = default)
    {
        RequireAuthenticated();

        return await _httpClient.GetFromJsonAsync<DirectConversationSummaryResponse[]>(
            "/api/v1/direct/summaries",
            cancellationToken)
            ?? Array.Empty<DirectConversationSummaryResponse>();
    }

    public async Task<MessageResponse> SendDirectMessageAsync(
        Guid conversationId,
        SendMessageRequest request,
        CancellationToken cancellationToken = default)
    {
        RequireAuthenticated();

        using var response = await _httpClient.PostAsJsonAsync(
            $"/api/v1/direct/{conversationId:D}/messages",
            request,
            cancellationToken);

        response.EnsureSuccessStatusCode();

        return await response.Content.ReadFromJsonAsync<MessageResponse>(
            cancellationToken: cancellationToken)
            ?? throw new InvalidDataException("Message response was empty.");
    }

    public async Task<MessagePageResponse> GetDirectHistoryPageAsync(
        Guid conversationId,
        Guid? afterMessageId = null,
        int limit = 50,
        CancellationToken cancellationToken = default)
    {
        RequireAuthenticated();

        var path = $"/api/v1/direct/{conversationId:D}/messages/page?limit={limit}";
        if (afterMessageId is { } cursor)
            path += $"&afterMessageId={cursor:D}";

        return await _httpClient.GetFromJsonAsync<MessagePageResponse>(
            path,
            cancellationToken)
            ?? throw new InvalidDataException("Message page response was empty.");
    }

    public async Task<IReadOnlyList<MessageReceiptResponse>> GetMessageReceiptsAsync(
        Guid messageId,
        CancellationToken cancellationToken = default)
    {
        RequireAuthenticated();

        return await _httpClient.GetFromJsonAsync<MessageReceiptResponse[]>(
            $"/api/v1/messages/{messageId:D}/receipts",
            cancellationToken)
            ?? Array.Empty<MessageReceiptResponse>();
    }

    public async Task MarkMessageDeliveredAsync(
        Guid messageId,
        CancellationToken cancellationToken = default)
    {
        RequireAuthenticated();

        using var response = await _httpClient.PostAsync(
            $"/api/v1/messages/{messageId:D}/delivered",
            content: null,
            cancellationToken);

        response.EnsureSuccessStatusCode();
    }

    public async Task MarkMessageReadAsync(
        Guid messageId,
        CancellationToken cancellationToken = default)
    {
        RequireAuthenticated();

        using var response = await _httpClient.PostAsync(
            $"/api/v1/messages/{messageId:D}/read",
            content: null,
            cancellationToken);

        response.EnsureSuccessStatusCode();
    }

    public async Task<RealtimeTicketResponse> CreateRealtimeTicketAsync(
        CancellationToken cancellationToken = default)
    {
        RequireAuthenticated();

        using var response = await _httpClient.PostAsync(
            "/api/v1/realtime/ticket",
            content: null,
            cancellationToken);

        response.EnsureSuccessStatusCode();

        return await response.Content.ReadFromJsonAsync<RealtimeTicketResponse>(
            cancellationToken: cancellationToken)
            ?? throw new InvalidDataException("Realtime ticket response was empty.");
    }

    public async Task<MessageResponse> EditDirectMessageAsync(
        Guid conversationId,
        Guid messageId,
        string body,
        CancellationToken cancellationToken = default)
    {
        RequireAuthenticated();

        using var response = await _httpClient.PutAsJsonAsync(
            $"/api/v1/direct/{conversationId:D}/messages/{messageId:D}",
            new EditMessageRequest(body),
            cancellationToken);

        response.EnsureSuccessStatusCode();

        return await response.Content.ReadFromJsonAsync<MessageResponse>(
            cancellationToken: cancellationToken)
            ?? throw new InvalidDataException("Edited message response was empty.");
    }

    public async Task<MessageDeletedResponse> DeleteDirectMessageAsync(
        Guid conversationId,
        Guid messageId,
        CancellationToken cancellationToken = default)
    {
        RequireAuthenticated();

        using var response = await _httpClient.DeleteAsync(
            $"/api/v1/direct/{conversationId:D}/messages/{messageId:D}",
            cancellationToken);

        response.EnsureSuccessStatusCode();

        return await response.Content.ReadFromJsonAsync<MessageDeletedResponse>(
            cancellationToken: cancellationToken)
            ?? throw new InvalidDataException("Deleted message response was empty.");
    }

    public async Task<ServerSettingsSnapshotResponse> GetServerSettingsAsync(
        CancellationToken cancellationToken = default)
    {
        RequireAuthenticated();

        return await _httpClient.GetFromJsonAsync<ServerSettingsSnapshotResponse>(
            "/api/v1/server/settings",
            cancellationToken)
            ?? throw new InvalidDataException("Server settings response was empty.");
    }

    public async Task<ServerSettingsSnapshotResponse> UpdateServerSettingsAsync(
        UpdateServerSettingsRequest request,
        CancellationToken cancellationToken = default)
    {
        RequireAuthenticated();

        using var response = await _httpClient.PutAsJsonAsync(
            "/api/v1/server/settings",
            request,
            cancellationToken);

        response.EnsureSuccessStatusCode();

        return await response.Content.ReadFromJsonAsync<ServerSettingsSnapshotResponse>(
            cancellationToken: cancellationToken)
            ?? throw new InvalidDataException("Server settings response was empty.");
    }

    public async Task<ConversationReadResponse> MarkDirectConversationReadAsync(
        Guid conversationId,
        Guid? upToMessageId = null,
        CancellationToken cancellationToken = default)
    {
        RequireAuthenticated();

        using var response = await _httpClient.PostAsJsonAsync(
            $"/api/v1/direct/{conversationId:D}/read",
            new MarkConversationReadRequest(upToMessageId),
            cancellationToken);

        response.EnsureSuccessStatusCode();

        return await response.Content.ReadFromJsonAsync<ConversationReadResponse>(
            cancellationToken: cancellationToken)
            ?? throw new InvalidDataException("Conversation read response was empty.");
    }

    public async Task LogoutAsync(
        CancellationToken cancellationToken = default)
    {
        RequireAuthenticated();

        using var response = await _httpClient.PostAsync(
            "/api/v1/auth/logout",
            content: null,
            cancellationToken);

        response.EnsureSuccessStatusCode();
        _httpClient.DefaultRequestHeaders.Authorization = null;
    }

    public async Task<IReadOnlyList<SessionSummaryResponse>> ListSessionsAsync(
        CancellationToken cancellationToken = default)
    {
        RequireAuthenticated();

        return await _httpClient.GetFromJsonAsync<SessionSummaryResponse[]>(
            "/api/v1/auth/sessions",
            cancellationToken)
            ?? Array.Empty<SessionSummaryResponse>();
    }

    public async Task RevokeSessionAsync(
        Guid sessionId,
        CancellationToken cancellationToken = default)
    {
        RequireAuthenticated();

        using var response = await _httpClient.PostAsync(
            $"/api/v1/auth/sessions/{sessionId:D}/revoke",
            content: null,
            cancellationToken);

        response.EnsureSuccessStatusCode();
    }

    private void RequireAuthenticated()
    {
        if (_httpClient.DefaultRequestHeaders.Authorization is null)
            throw new InvalidOperationException("An authenticated session is required.");
    }
}
