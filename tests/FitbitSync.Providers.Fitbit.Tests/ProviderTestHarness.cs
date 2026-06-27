using FitbitSync.Domain;
using FitbitSync.Providers.Fitbit;

namespace FitbitSync.Providers.Fitbit.Tests;

// Test plumbing: builds a FitbitApiClient (bearer + rate-limit handler chain) pointed at a base URL,
// with a stub access-token source and a fixed clock, plus a FitbitHealthDataProvider over it.
internal static class ProviderTestHarness
{
    public static readonly DateTimeOffset FixedNow = new(2024, 5, 10, 12, 0, 0, TimeSpan.Zero);

    public static (FitbitHealthDataProvider Provider, FitbitApiClient Client) Build(string baseUrl)
    {
        var holder = new RateLimitSnapshotHolder();

        var handlerChain = new BearerTokenHandler(new StubAccessTokenSource())
        {
            InnerHandler = new RateLimitHandler(holder, new FixedClock())
            {
                InnerHandler = new HttpClientHandler(),
            },
        };

        var httpClient = new HttpClient(handlerChain) { BaseAddress = new Uri(baseUrl) };
        var client = new FitbitApiClient(httpClient, holder);

        return (new FitbitHealthDataProvider(client), client);
    }

    private sealed class FixedClock : IClock
    {
        public DateTimeOffset UtcNow => FixedNow;
    }

    private sealed class StubAccessTokenSource : IAccessTokenSource
    {
        public Task<string> GetAccessTokenAsync(CancellationToken ct = default) => Task.FromResult("fake-access-token");

        public Task<string> RefreshAccessTokenAsync(string knownBadToken, CancellationToken ct = default)
            => this.GetAccessTokenAsync(ct);
    }
}
