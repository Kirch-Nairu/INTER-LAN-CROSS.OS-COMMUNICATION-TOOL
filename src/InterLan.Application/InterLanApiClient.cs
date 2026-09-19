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

    private void RequireAuthenticated()
    {
        if (_httpClient.DefaultRequestHeaders.Authorization is null)
            throw new InvalidOperationException("An authenticated session is required.");
    }
}
