using System.Net.Http.Json;
using FitbitSync.Domain;

namespace FitbitSync.Providers.GoogleHealth;

// Exchanges authorization codes and refreshes tokens against the Google OAuth token endpoint using the
// confidential web client (client_id + client_secret). Maps the response to the domain OAuthToken. A
// refresh response may omit refresh_token, in which case the existing refresh token is retained.
public sealed class GoogleTokenClient
{
    private readonly HttpClient httpClient;
    private readonly GoogleOAuthOptions options;
    private readonly IClock clock;

    public GoogleTokenClient(HttpClient httpClient, GoogleOAuthOptions options, IClock clock)
    {
        ArgumentNullException.ThrowIfNull(httpClient);
        ArgumentNullException.ThrowIfNull(options);
        ArgumentNullException.ThrowIfNull(clock);

        this.httpClient = httpClient;
        this.options = options;
        this.clock = clock;
    }

    public Task<OAuthToken> ExchangeCodeAsync(string code, CancellationToken ct = default)
    {
        ArgumentException.ThrowIfNullOrEmpty(code);

        if (this.options.RedirectUri is null)
        {
            throw new InvalidOperationException("GoogleOAuthOptions.RedirectUri must be set to exchange the authorization code.");
        }

        var form = new Dictionary<string, string>
        {
            ["grant_type"] = "authorization_code",
            ["code"] = code,
            ["client_id"] = this.options.ClientId,
            ["client_secret"] = this.options.ClientSecret,
            ["redirect_uri"] = this.options.RedirectUri.ToString(),
        };

        return this.PostAsync(form, existingRefreshToken: null, ct);
    }

    public Task<OAuthToken> RefreshAsync(string refreshToken, CancellationToken ct = default)
    {
        ArgumentException.ThrowIfNullOrEmpty(refreshToken);

        var form = new Dictionary<string, string>
        {
            ["grant_type"] = "refresh_token",
            ["refresh_token"] = refreshToken,
            ["client_id"] = this.options.ClientId,
            ["client_secret"] = this.options.ClientSecret,
        };

        return this.PostAsync(form, existingRefreshToken: refreshToken, ct);
    }

    private async Task<OAuthToken> PostAsync(Dictionary<string, string> form, string? existingRefreshToken, CancellationToken ct)
    {
        using var content = new FormUrlEncodedContent(form);
        using var response = await this.httpClient.PostAsync(this.options.TokenEndpoint, content, ct).ConfigureAwait(false);

        if (!response.IsSuccessStatusCode)
        {
            var error = await response.Content.ReadAsStringAsync(ct).ConfigureAwait(false);
            throw new GoogleAuthenticationException($"Google token endpoint returned {(int)response.StatusCode}: {error}");
        }

        var dto = await response.Content.ReadFromJsonAsync<GoogleTokenResponse>(ct).ConfigureAwait(false)
                  ?? throw new GoogleAuthenticationException("Google token endpoint returned an empty body.");

        var refreshToken = string.IsNullOrEmpty(dto.RefreshToken) ? existingRefreshToken ?? "" : dto.RefreshToken;
        var scopes = dto.Scope?.Split(' ', StringSplitOptions.RemoveEmptyEntries) ?? [];

        return new OAuthToken(dto.AccessToken, refreshToken, this.clock.UtcNow.AddSeconds(dto.ExpiresIn), scopes);
    }
}
