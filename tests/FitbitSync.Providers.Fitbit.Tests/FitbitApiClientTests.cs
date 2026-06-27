using System.Text.Json.Serialization;
using FitbitSync.Domain;
using FitbitSync.Providers.Fitbit;
using FluentAssertions;
using WireMock.RequestBuilders;
using WireMock.ResponseBuilders;
using WireMock.Server;

namespace FitbitSync.Providers.Fitbit.Tests;

// Phase 3 (3e): the typed Fitbit client, driven through its bearer + rate-limit handlers against a
// WireMock server, must deserialize the JSON body AND capture the Fitbit-Rate-Limit-* headers into a
// RateLimitSnapshot. Reset is seconds-until-reset, resolved to an instant via the injected IClock.
public sealed class FitbitApiClientTests : IDisposable
{
    private readonly WireMockServer server = WireMockServer.Start();

    private sealed record Payload([property: JsonPropertyName("hello")] string Hello);

    private sealed class FixedClock : IClock
    {
        public DateTimeOffset UtcNow { get; } = new(2024, 5, 10, 12, 0, 0, TimeSpan.Zero);
    }

    private sealed class StubAccessTokenSource : IAccessTokenSource
    {
        public Task<string> GetAccessTokenAsync(CancellationToken ct = default) => Task.FromResult("fake-access-token");

        public Task<string> RefreshAccessTokenAsync(string knownBadToken, CancellationToken ct = default)
            => this.GetAccessTokenAsync(ct);
    }

    [Fact]
    public async Task FitbitApiClient_CapturesRateLimitSnapshot_FromHeaders()
    {
        // Given a WireMock endpoint returning a JSON body plus the three Fitbit rate-limit headers...
        this.server
            .Given(Request.Create().WithPath("/probe.json").UsingGet())
            .RespondWith(Response.Create()
                .WithStatusCode(200)
                .WithHeader("Content-Type", "application/json")
                .WithHeader("Fitbit-Rate-Limit-Limit", "150")
                .WithHeader("Fitbit-Rate-Limit-Remaining", "123")
                .WithHeader("Fitbit-Rate-Limit-Reset", "1800")
                .WithBody("""{ "hello": "world" }"""));

        var holder = new RateLimitSnapshotHolder();
        var clock = new FixedClock();

        var handlerChain = new BearerTokenHandler(new StubAccessTokenSource())
        {
            InnerHandler = new RateLimitHandler(holder, clock)
            {
                InnerHandler = new HttpClientHandler(),
            },
        };

        using var httpClient = new HttpClient(handlerChain) { BaseAddress = new Uri(this.server.Urls[0]) };
        var client = new FitbitApiClient(httpClient, holder);

        // When a JSON GET is issued through the typed client...
        var payload = await client.GetJsonAsync<Payload>("probe.json");

        // Then the body deserializes...
        payload.Hello.Should().Be("world");

        // ...and the captured snapshot reflects the headers, with Reset resolved via the clock.
        client.LatestRateLimit.Should().NotBeNull();
        client.LatestRateLimit!.Limit.Should().Be(150);
        client.LatestRateLimit.Remaining.Should().Be(123);
        client.LatestRateLimit.ResetSeconds.Should().Be(1800);
        client.LatestRateLimit.ObservedAt.Should().Be(clock.UtcNow);
        client.LatestRateLimit.ResetsAt.Should().Be(clock.UtcNow.AddSeconds(1800));
    }

    public void Dispose() => this.server.Dispose();
}
