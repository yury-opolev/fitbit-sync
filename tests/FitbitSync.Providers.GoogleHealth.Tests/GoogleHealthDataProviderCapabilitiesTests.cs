using FitbitSync.Domain;
using FluentAssertions;

namespace FitbitSync.Providers.GoogleHealth.Tests;

// Capabilities drive generic backfill planning, so the advertised set is a contract. The GoogleHealth
// provider must advertise EXACTLY the metrics the catalog can resolve (IsSupported) — no more, no less —
// otherwise planning would request a metric the fetch path then throws on, or silently skip a metric we
// actually map. Mirrors FitbitProviderCapabilitiesTests, but pins the set against the catalog rather than
// a hand-written literal so the two cannot drift apart.
public sealed class GoogleHealthDataProviderCapabilitiesTests
{
    [Fact]
    public void Provider_SourceIsGoogle()
    {
        BuildProvider().Source.Should().Be("google");
    }

    [Fact]
    public void Provider_AdvertisesExactlyTheCatalogSupportedMetrics()
    {
        var advertised = BuildProvider().Capabilities.Select(capability => capability.Metric);

        var catalogSupported = Enum.GetValues<MetricType>()
            .Where(GoogleHealthDataTypeCatalog.IsSupported);

        advertised.Should().BeEquivalentTo(catalogSupported);
    }

    [Fact]
    public void Provider_AdvertisedResolutionsMatchTheCatalog()
    {
        foreach (var capability in BuildProvider().Capabilities)
        {
            capability.Resolution.Should().Be(
                GoogleHealthDataTypeCatalog.Resolve(capability.Metric).Resolution,
                $"the advertised resolution for {capability.Metric} must match its catalog descriptor");
        }
    }

    private static GoogleHealthDataProvider BuildProvider()
    {
        // Capabilities/Source make no network call, so a non-routable client and a never-loaded token store suffice.
        var options = new GoogleOAuthOptions
        {
            ClientId = "c",
            ClientSecret = "s",
            RedirectUri = new Uri("https://localhost:7654/callback"),
        };
        var clock = new FixedClock();
        var accessTokenSource = new GoogleAccessTokenSource(
            new NullTokenStore(),
            new GoogleTokenClient(new HttpClient(), options, clock),
            clock,
            options);
        var httpClient = new HttpClient { BaseAddress = GoogleHealthApiClient.BaseAddress };
        return new GoogleHealthDataProvider(new GoogleHealthApiClient(httpClient, accessTokenSource));
    }

    private sealed class FixedClock : IClock
    {
        public DateTimeOffset UtcNow { get; } = new(2026, 6, 27, 12, 0, 0, TimeSpan.Zero);
    }

    private sealed class NullTokenStore : ITokenStore
    {
        public Task<OAuthToken?> LoadAsync(CancellationToken ct = default) => Task.FromResult<OAuthToken?>(null);

        public Task SaveAsync(OAuthToken token, CancellationToken ct = default) => Task.CompletedTask;
    }
}
