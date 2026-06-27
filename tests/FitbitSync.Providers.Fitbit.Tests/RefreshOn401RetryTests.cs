using System.Net;
using FitbitSync.Domain;
using FluentAssertions;
using WireMock.RequestBuilders;
using WireMock.ResponseBuilders;
using WireMock.Server;

namespace FitbitSync.Providers.Fitbit.Tests;

// Phase 4 (4c): a 401 on a Fitbit data call must drive the full reactive path end-to-end —
// BearerTokenHandler force-refreshes through RefreshingAccessTokenSource (single-flight + persist +
// audit) and retries the original GET EXACTLY ONCE with the rotated token. The seeded token is well
// outside the RefreshSkew window, so the refresh can ONLY originate from the 401 path, not the proactive one.
public sealed class RefreshOn401RetryTests : IDisposable
{
    private const string TokenPath = "/oauth2/token";
    private const string DataPath = "/1/user/-/profile.json";

    private readonly WireMockServer server = WireMockServer.Start();

    private sealed class FixedClock : IClock
    {
        public DateTimeOffset UtcNow { get; } = new(2024, 5, 10, 12, 0, 0, TimeSpan.Zero);
    }

    private FitbitOAuthOptions BuildOptions() => new()
    {
        ClientId = "test-client",
        ClientSecret = null,
        RefreshSkew = TimeSpan.FromMinutes(5),
        TokenEndpoint = new Uri(this.server.Urls[0] + TokenPath),
    };

    [Fact]
    public async Task DataCall_Gets401_ThenForceRefreshes_AndRetriesOnceSuccessfully()
    {
        // Token endpoint returns a rotated pair.
        this.server
            .Given(Request.Create().WithPath(TokenPath).UsingPost())
            .RespondWith(Response.Create()
                .WithStatusCode(200)
                .WithHeader("Content-Type", "application/json")
                .WithBody("""
                    {
                        "access_token": "rotated-access",
                        "refresh_token": "rotated-refresh",
                        "expires_in": 28800,
                        "token_type": "Bearer",
                        "scope": "heartrate",
                        "user_id": "ABC"
                    }
                    """));

        // Data endpoint: 401 on the first call, 200 on the retry — driven by scenario state.
        const string scenario = "data-401-then-200";
        this.server
            .Given(Request.Create().WithPath(DataPath).UsingGet())
            .InScenario(scenario)
            .WillSetStateTo("refreshed")
            .RespondWith(Response.Create().WithStatusCode(401));

        this.server
            .Given(Request.Create().WithPath(DataPath).UsingGet())
            .InScenario(scenario)
            .WhenStateIs("refreshed")
            .RespondWith(Response.Create()
                .WithStatusCode(200)
                .WithHeader("Content-Type", "application/json")
                .WithBody("""{"ok":true}"""));

        var clock = new FixedClock();
        var options = this.BuildOptions();

        // Seed a token that is NOT within the skew window, so only the 401 path can trigger a refresh.
        var seededToken = new OAuthToken(
            "stale-access",
            "old-refresh",
            clock.UtcNow + TimeSpan.FromHours(1),
            ["heartrate"]);

        var store = new FakeTokenStore(seededToken);
        var audit = new CountingAuditTrail();
        var coordinator = new TokenRefreshCoordinator();
        var tokenClient = new FitbitTokenClient(new HttpClient(), options, clock);
        var source = new RefreshingAccessTokenSource(store, tokenClient, coordinator, audit, clock, options);

        var handler = new BearerTokenHandler(source) { InnerHandler = new HttpClientHandler() };
        using var dataClient = new HttpClient(handler) { BaseAddress = new Uri(this.server.Urls[0]) };

        // Act.
        var resp = await dataClient.GetAsync(DataPath);

        // Assert: the retry succeeded...
        resp.StatusCode.Should().Be(HttpStatusCode.OK);

        // ...the rotated token was refreshed + persisted exactly once...
        store.Current!.AccessToken.Should().Be("rotated-access");
        store.Current!.RefreshToken.Should().Be("rotated-refresh");
        store.SaveCount.Should().Be(1);

        // ...a single audit entry was written...
        audit.AppendCount.Should().Be(1);

        // ...the data endpoint saw the 401 then the 200 (2 requests), and the token endpoint saw 1 POST.
        this.server.LogEntries.Count(e => e.RequestMessage.Path == DataPath).Should().Be(2);
        this.server.LogEntries.Count(e => e.RequestMessage.Path == TokenPath).Should().Be(1);
    }

    public void Dispose() => this.server.Dispose();

    private sealed class FakeTokenStore : ITokenStore
    {
        private readonly object gate = new();

        public FakeTokenStore(OAuthToken? seed)
        {
            this.Current = seed;
        }

        public OAuthToken? Current { get; private set; }

        public int SaveCount { get; private set; }

        public Task<OAuthToken?> LoadAsync(CancellationToken ct = default)
        {
            lock (this.gate)
            {
                return Task.FromResult(this.Current);
            }
        }

        public Task SaveAsync(OAuthToken token, CancellationToken ct = default)
        {
            lock (this.gate)
            {
                this.Current = token;
                this.SaveCount++;
            }

            return Task.CompletedTask;
        }
    }

    private sealed class CountingAuditTrail : IAuditTrail
    {
        public int AppendCount;

        public Task<AuditEntry> AppendAsync(string action, CancellationToken ct = default)
        {
            Interlocked.Increment(ref this.AppendCount);
            return Task.FromResult(new AuditEntry(this.AppendCount, DateTimeOffset.UnixEpoch, action, "", ""));
        }

        public Task<bool> VerifyChainAsync(CancellationToken ct = default) => Task.FromResult(true);
    }
}
