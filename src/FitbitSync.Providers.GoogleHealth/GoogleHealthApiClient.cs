using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using FitbitSync.Domain;

namespace FitbitSync.Providers.GoogleHealth;

// Typed HttpClient for the Google Health REST API (base https://health.googleapis.com). Attaches the
// bearer access token per request; on a 401 it force-refreshes the token and retries once. A 429 surfaces
// as the neutral ProviderRateLimitedException; a persistent 401 as GoogleAuthenticationException.
public sealed class GoogleHealthApiClient
{
    public static readonly Uri BaseAddress = new("https://health.googleapis.com");

    private readonly HttpClient httpClient;
    private readonly GoogleAccessTokenSource accessTokenSource;

    public GoogleHealthApiClient(HttpClient httpClient, GoogleAccessTokenSource accessTokenSource)
    {
        ArgumentNullException.ThrowIfNull(httpClient);
        ArgumentNullException.ThrowIfNull(accessTokenSource);

        this.httpClient = httpClient;
        this.accessTokenSource = accessTokenSource;
    }

    public async Task<T> GetJsonAsync<T>(string relativePath, CancellationToken ct = default)
    {
        var response = await this.SendAsync(relativePath, forceRefresh: false, ct).ConfigureAwait(false);

        if (response.StatusCode == HttpStatusCode.Unauthorized)
        {
            response.Dispose();
            response = await this.SendAsync(relativePath, forceRefresh: true, ct).ConfigureAwait(false);
        }

        using (response)
        {
            if (response.StatusCode == HttpStatusCode.TooManyRequests)
            {
                throw new ProviderRateLimitedException("Google Health API returned 429 Too Many Requests.", null);
            }

            if (response.StatusCode == HttpStatusCode.Unauthorized)
            {
                throw new GoogleAuthenticationException("Google Health API returned 401 Unauthorized; re-authorize with `login`.");
            }

            if (!response.IsSuccessStatusCode)
            {
                var errorBody = await response.Content.ReadAsStringAsync(ct).ConfigureAwait(false);
                throw new HttpRequestException($"Google Health {(int)response.StatusCode} for '{relativePath}': {errorBody}");
            }

            var payload = await response.Content.ReadFromJsonAsync<T>(ct).ConfigureAwait(false);
            return payload ?? throw new InvalidOperationException($"Google Health response body for '{relativePath}' was empty.");
        }
    }

    private async Task<HttpResponseMessage> SendAsync(string relativePath, bool forceRefresh, CancellationToken ct)
    {
        var token = forceRefresh
            ? await this.accessTokenSource.ForceRefreshAsync(ct).ConfigureAwait(false)
            : await this.accessTokenSource.GetAccessTokenAsync(ct).ConfigureAwait(false);

        using var request = new HttpRequestMessage(HttpMethod.Get, relativePath);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        return await this.httpClient.SendAsync(request, ct).ConfigureAwait(false);
    }
}
