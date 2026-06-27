namespace FitbitSync.Providers.Fitbit;

public interface IAccessTokenSource
{
    Task<string> GetAccessTokenAsync(CancellationToken ct = default);

    // Reactive refresh after a 401: force a coordinated refresh unless another caller
    // already rotated past knownBadToken. Returns the current valid access token.
    Task<string> RefreshAccessTokenAsync(string knownBadToken, CancellationToken ct = default);
}
