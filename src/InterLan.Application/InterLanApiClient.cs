using System.Net.Http.Headers;

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

    private void RequireAuthenticated()
    {
        if (_httpClient.DefaultRequestHeaders.Authorization is null)
            throw new InvalidOperationException("An authenticated session is required.");
    }
}
