using FitbitSync.Domain;

namespace FitbitSync.Providers.Fitbit;

public sealed class TokenStoreAccessTokenSource : IAccessTokenSource
{
    private readonly ITokenStore tokenStore;

    public TokenStoreAccessTokenSource(ITokenStore tokenStore)
    {
        ArgumentNullException.ThrowIfNull(tokenStore);
        this.tokenStore = tokenStore;
    }

    public async Task<string> GetAccessTokenAsync(CancellationToken ct = default)
    {
        var token = await this.tokenStore.LoadAsync(ct).ConfigureAwait(false);

        if (token is null)
        {
            throw new FitbitAuthenticationException("No OAuth token is stored; authorization is required.");
        }

        return token.AccessToken;
    }

    public Task<string> RefreshAccessTokenAsync(string knownBadToken, CancellationToken ct = default)
        => throw new FitbitAuthenticationException(
            "The token store is read-only and cannot refresh on 401; re-authorization is required.");
}
