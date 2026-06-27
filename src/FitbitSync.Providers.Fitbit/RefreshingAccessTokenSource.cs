using FitbitSync.Domain;

namespace FitbitSync.Providers.Fitbit;

public sealed class RefreshingAccessTokenSource : IAccessTokenSource
{
    private const string CoordinatorKey = "fitbit";

    private readonly ITokenStore store;
    private readonly FitbitTokenClient tokenClient;
    private readonly ITokenRefreshCoordinator coordinator;
    private readonly IAuditTrail audit;
    private readonly IClock clock;
    private readonly FitbitOAuthOptions options;

    public RefreshingAccessTokenSource(
        ITokenStore store,
        FitbitTokenClient tokenClient,
        ITokenRefreshCoordinator coordinator,
        IAuditTrail audit,
        IClock clock,
        FitbitOAuthOptions options)
    {
        this.store = store;
        this.tokenClient = tokenClient;
        this.coordinator = coordinator;
        this.audit = audit;
        this.clock = clock;
        this.options = options;
    }

    public async Task<string> GetAccessTokenAsync(CancellationToken ct = default)
    {
        var token = await this.store.LoadAsync(ct).ConfigureAwait(false)
            ?? throw new FitbitAuthenticationException("No Fitbit token is stored; authorization is required.");

        if (!this.NeedsRefresh(token))
        {
            return token.AccessToken;
        }

        // Single-flight: only one refresh per process at a time for this provider.
        return await this.coordinator.RunSingleFlightAsync(CoordinatorKey, async innerCt =>
        {
            // Re-load inside the gate: a concurrent caller may have already refreshed.
            var current = await this.store.LoadAsync(innerCt).ConfigureAwait(false)
                ?? throw new FitbitAuthenticationException("No Fitbit token is stored; authorization is required.");

            if (!this.NeedsRefresh(current))
            {
                return current.AccessToken;
            }

            var refreshed = await this.tokenClient.RefreshAsync(current.RefreshToken, innerCt).ConfigureAwait(false);

            // Persist-before-discard: the new token is only "real" once SaveAsync commits.
            await this.store.SaveAsync(refreshed, innerCt).ConfigureAwait(false);
            await this.audit.AppendAsync("TokenRefresh", innerCt).ConfigureAwait(false);

            return refreshed.AccessToken;
        }, ct).ConfigureAwait(false);
    }

    public async Task<string> RefreshAccessTokenAsync(string knownBadToken, CancellationToken ct = default)
    {
        return await this.coordinator.RunSingleFlightAsync(CoordinatorKey, async innerCt =>
        {
            // Re-load inside the gate: a concurrent 401 handler may have already rotated the token.
            var current = await this.store.LoadAsync(innerCt).ConfigureAwait(false)
                ?? throw new FitbitAuthenticationException("No Fitbit token is stored; authorization is required.");

            // If the stored token is no longer the one that got the 401, someone already refreshed — use theirs.
            if (current.AccessToken != knownBadToken)
            {
                return current.AccessToken;
            }

            var refreshed = await this.tokenClient.RefreshAsync(current.RefreshToken, innerCt).ConfigureAwait(false);

            // Persist-before-discard, then audit (same ordering as the proactive path).
            await this.store.SaveAsync(refreshed, innerCt).ConfigureAwait(false);
            await this.audit.AppendAsync("TokenRefresh", innerCt).ConfigureAwait(false);

            return refreshed.AccessToken;
        }, ct).ConfigureAwait(false);
    }

    private bool NeedsRefresh(OAuthToken token)
        => token.IsExpired(this.clock.UtcNow + this.options.RefreshSkew);

    // Phase 4 future item: cross-process refresh races are not guarded here (single-process tool).
    // If multiple processes ever refresh concurrently, add RowVersion optimistic concurrency to
    // ITokenStore.SaveAsync and reload-on-conflict instead of re-refreshing.
}
