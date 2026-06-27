using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using FitbitSync.Domain;

namespace FitbitSync.Providers.Fitbit;

public sealed class FitbitTokenClient
{
    private readonly HttpClient httpClient;
    private readonly FitbitOAuthOptions options;
    private readonly IClock clock;

    public FitbitTokenClient(HttpClient httpClient, FitbitOAuthOptions options, IClock clock)
    {
        ArgumentNullException.ThrowIfNull(httpClient);
        ArgumentNullException.ThrowIfNull(options);
        ArgumentNullException.ThrowIfNull(clock);

        this.httpClient = httpClient;
        this.options = options;
        this.clock = clock;
    }

    public Task<OAuthToken> RefreshAsync(string refreshToken, CancellationToken ct = default)
    {
        var form = new Dictionary<string, string>
        {
            ["grant_type"] = "refresh_token",
            ["refresh_token"] = refreshToken,
        };

        if (string.IsNullOrEmpty(this.options.ClientSecret))
        {
            form["client_id"] = this.options.ClientId;
        }

        return this.PostAsync(form, ct);
    }

    public Task<OAuthToken> ExchangeCodeAsync(string code, string codeVerifier, string redirectUri, CancellationToken ct = default)
    {
        var form = new Dictionary<string, string>
        {
            ["grant_type"] = "authorization_code",
            ["code"] = code,
            ["code_verifier"] = codeVerifier,
            ["redirect_uri"] = redirectUri,
        };

        if (string.IsNullOrEmpty(this.options.ClientSecret))
        {
            form["client_id"] = this.options.ClientId;
        }

        return this.PostAsync(form, ct);
    }

    private async Task<OAuthToken> PostAsync(Dictionary<string, string> form, CancellationToken ct)
    {
        using var content = new FormUrlEncodedContent(form);
        using var request = new HttpRequestMessage(HttpMethod.Post, this.options.TokenEndpoint) { Content = content };

        if (!string.IsNullOrEmpty(this.options.ClientSecret))
        {
            var basic = Convert.ToBase64String(Encoding.UTF8.GetBytes($"{this.options.ClientId}:{this.options.ClientSecret}"));
            request.Headers.Authorization = new AuthenticationHeaderValue("Basic", basic);
        }

        using var response = await this.httpClient.SendAsync(request, ct).ConfigureAwait(false);

        if (!response.IsSuccessStatusCode)
        {
            var body = await response.Content.ReadAsStringAsync(ct).ConfigureAwait(false);
            throw new FitbitAuthenticationException($"Fitbit token endpoint returned {(int)response.StatusCode}: {body}");
        }

        var dto = await response.Content.ReadFromJsonAsync<FitbitTokenResponse>(ct).ConfigureAwait(false)
                  ?? throw new FitbitAuthenticationException("Fitbit token endpoint returned an empty body.");

        return new OAuthToken(
            dto.AccessToken,
            dto.RefreshToken,
            this.clock.UtcNow.AddSeconds(dto.ExpiresIn),
            dto.Scope?.Split(' ', StringSplitOptions.RemoveEmptyEntries) ?? []);
    }
}
