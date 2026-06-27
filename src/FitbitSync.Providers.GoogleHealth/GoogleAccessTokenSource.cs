using FitbitSync.Domain;

namespace FitbitSync.Providers.GoogleHealth;

// Supplies a valid Google access token for Health API calls: loads the stored token, proactively refreshes
// it (within the configured skew) via the Google token endpoint, and persists the refreshed token. Exposes
// a force-refresh for the reactive 401 path. Throws GoogleAuthenticationException when no usable token or
// refresh token is available (re-authorization required).
public sealed class GoogleAccessTokenSource
{
    private readonly ITokenStore store;
    private readonly GoogleTokenClient tokenClient;
    private readonly IClock clock;
    private readonly GoogleOAuthOptions options;

    public GoogleAccessTokenSource(ITokenStore store, GoogleTokenClient tokenClient, IClock clock, GoogleOAuthOptions options)
    {
        ArgumentNullException.ThrowIfNull(store);
        ArgumentNullException.ThrowIfNull(tokenClient);
        ArgumentNullException.ThrowIfNull(clock);
        ArgumentNullException.ThrowIfNull(options);

        this.store = store;
        this.tokenClient = tokenClient;
        this.clock = clock;
        this.options = options;
    }

    public async Task<string> GetAccessTokenAsync(CancellationToken ct = default)
    {
        var token = await this.LoadAsync(ct).ConfigureAwait(false);

        if (token.ExpiresAt <= this.clock.UtcNow + this.options.RefreshSkew)
        {
            token = await this.RefreshAsync(token, ct).ConfigureAwait(false);
        }

        return token.AccessToken;
    }

    public async Task<string> ForceRefreshAsync(CancellationToken ct = default)
    {
        var token = await this.LoadAsync(ct).ConfigureAwait(false);
        token = await this.RefreshAsync(token, ct).ConfigureAwait(false);
        return token.AccessToken;
    }

    private async Task<OAuthToken> LoadAsync(CancellationToken ct) =>
        await this.store.LoadAsync(ct).ConfigureAwait(false)
        ?? throw new GoogleAuthenticationException("No stored Google token; run `login --begin` then `login --complete`.");

    private async Task<OAuthToken> RefreshAsync(OAuthToken token, CancellationToken ct)
    {
        if (string.IsNullOrEmpty(token.RefreshToken))
        {
            throw new GoogleAuthenticationException("The stored Google token has no refresh token; re-authorize with `login`.");
        }

        var refreshed = await this.tokenClient.RefreshAsync(token.RefreshToken, ct).ConfigureAwait(false);
        await this.store.SaveAsync(refreshed, ct).ConfigureAwait(false);
        return refreshed;
    }
}
