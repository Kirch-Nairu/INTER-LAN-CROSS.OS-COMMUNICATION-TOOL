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

    private void RequireAuthenticated()
    {
        if (_httpClient.DefaultRequestHeaders.Authorization is null)
            throw new InvalidOperationException("An authenticated session is required.");
    }
}
