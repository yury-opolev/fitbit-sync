using FitbitSync.Domain;
using FluentAssertions;
using WireMock.RequestBuilders;
using WireMock.ResponseBuilders;
using WireMock.Server;

namespace FitbitSync.Providers.Fitbit.Tests;

// Phase 4 (4b): RefreshingAccessTokenSource refreshes an expired (within RefreshSkew) token, persisting the
// rotated access+refresh pair before returning. Concurrent callers must collapse onto a single refresh via the
// TokenRefreshCoordinator's single-flight gate: exactly one token-endpoint POST, one SaveAsync, one audit append.
public sealed class RefreshingAccessTokenSourceTests : IDisposable
{
    private const string TokenPath = "/oauth2/token";

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
    public async Task TokenService_RefreshesRotatedTokens_AtomicallyAndSingleFlight()
    {
        // Arrange: token endpoint returns a rotated pair, with a delay so concurrent callers queue on the gate.
        this.server
            .Given(Request.Create().WithPath(TokenPath).UsingPost())
            .RespondWith(Response.Create()
                .WithStatusCode(200)
                .WithHeader("Content-Type", "application/json")
                .WithDelay(TimeSpan.FromMilliseconds(200))
                .WithBody("""
                    {
                        "access_token": "rotated-access",
                        "refresh_token": "rotated-refresh",
                        "expires_in": 28800,
                        "token_type": "Bearer"
                    }
                    """));

        var clock = new FixedClock();
        var options = this.BuildOptions();

        var seededExpiredToken = new OAuthToken(
            "old-access",
            "old-refresh",
            clock.UtcNow - TimeSpan.FromMinutes(1),
            ["heartrate"]);

        var store = new FakeTokenStore(seededExpiredToken);
        var audit = new CountingAuditTrail();
        var coordinator = new TokenRefreshCoordinator();
        var tokenClient = new FitbitTokenClient(new HttpClient(), options, clock);
        var sut = new RefreshingAccessTokenSource(store, tokenClient, coordinator, audit, clock, options);

        // Act: fire 10 concurrent token requests.
        var tasks = Enumerable.Range(0, 10)
            .Select(_ => sut.GetAccessTokenAsync(CancellationToken.None))
            .ToArray();
        var results = await Task.WhenAll(tasks);

        // Assert: every caller got the rotated access token...
        results.Should().OnlyContain(t => t == "rotated-access");

        // ...the rotation was persisted exactly once...
        store.Current!.RefreshToken.Should().Be("rotated-refresh");
        store.SaveCount.Should().Be(1);

        // ...a single audit entry was appended...
        audit.AppendCount.Should().Be(1);

        // ...and the token endpoint received exactly one POST (single-flight collapse).
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
