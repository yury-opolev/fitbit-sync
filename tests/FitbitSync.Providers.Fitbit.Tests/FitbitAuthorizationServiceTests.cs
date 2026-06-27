using System.Security.Cryptography;
using System.Text;
using FitbitSync.Domain;
using FluentAssertions;
using WireMock.RequestBuilders;
using WireMock.ResponseBuilders;
using WireMock.Server;
using Xunit;

namespace FitbitSync.Providers.Fitbit.Tests;

// Phase 4 (4f): FitbitAuthorizationService drives the PKCE login flow offline. Begin() composes the
// consent URL (challenge + anti-CSRF state) and returns the verifier the caller holds. CompleteAsync()
// rejects a mismatched state (CSRF guard) BEFORE any network/persistence, and on a match exchanges the
// code, persists the token (persist-before-audit), and records exactly one "AuthGrant" audit entry.
public sealed class FitbitAuthorizationServiceTests : IDisposable
{
    private const string TokenPath = "/oauth2/token";

    private readonly WireMockServer server = WireMockServer.Start();

    [Fact]
    public void Begin_ComposesAuthorizeUrl_WithStateAndMatchingChallenge_AndReturnsVerifier()
    {
        var options = new FitbitOAuthOptions
        {
            ClientId = "test-client",
            RedirectUri = new Uri("http://127.0.0.1:7890/v1/oauth/callback"),
            Scopes = new[] { "heartrate", "sleep" },
        };
        var service = BuildService(new FakeTokenStore(seed: null), new RecordingAuditTrail(), options);

        var session = service.Begin();

        session.AuthorizeUrl.GetLeftPart(UriPartial.Path).Should().Be("https://www.fitbit.com/oauth2/authorize");
        var q = System.Web.HttpUtility.ParseQueryString(session.AuthorizeUrl.Query);
        q["state"].Should().Be(session.State);
        q["code_challenge_method"].Should().Be("S256");
        session.State.Should().NotBeNullOrEmpty();
        session.CodeVerifier.Should().NotBeNullOrEmpty();

        // The challenge in the URL must be the S256 derivation of the verifier we handed back.
        var expectedChallenge = Base64UrlEncoder.Encode(SHA256.HashData(Encoding.ASCII.GetBytes(session.CodeVerifier)));
        q["code_challenge"].Should().Be(expectedChallenge);

        // State and verifier are derived from distinct random draws, so they must not coincide.
        session.State.Should().NotBe(session.CodeVerifier);
    }

    [Fact]
    public async Task CompleteAsync_WithMatchingState_ExchangesPersistsAndAuditsAuthGrant()
    {
        StubTokenEndpoint();
        var store = new FakeTokenStore(seed: null);
        var audit = new RecordingAuditTrail();
        var options = BuildExchangeOptions();
        var service = BuildService(store, audit, options);
        var session = new FitbitAuthorizationSession(new Uri("https://www.fitbit.com/oauth2/authorize"), "state-abc", "verifier-xyz");

        var token = await service.CompleteAsync(session, returnedState: "state-abc", code: "auth-code-123", CancellationToken.None);

        token.AccessToken.Should().Be("new-access");
        token.RefreshToken.Should().Be("new-refresh");
        store.SaveCount.Should().Be(1);
        store.Current.Should().BeSameAs(token);
        audit.Actions.Should().ContainSingle().Which.Should().Be("AuthGrant");

        var body = this.server.LogEntries.Single().RequestMessage.Body;
        body.Should().Contain("grant_type=authorization_code");
        body.Should().Contain("code=auth-code-123");
        body.Should().Contain("code_verifier=verifier-xyz");
    }

    [Fact]
    public async Task CompleteAsync_WithStateMismatch_Throws_AndDoesNotExchangePersistOrAudit()
    {
        StubTokenEndpoint();
        var store = new FakeTokenStore(seed: null);
        var audit = new RecordingAuditTrail();
        var options = BuildExchangeOptions();
        var service = BuildService(store, audit, options);
        var session = new FitbitAuthorizationSession(new Uri("https://www.fitbit.com/oauth2/authorize"), "state-abc", "verifier-xyz");

        var act = async () => await service.CompleteAsync(session, returnedState: "WRONG-state", code: "auth-code-123", CancellationToken.None);

        await act.Should().ThrowAsync<FitbitAuthenticationException>();
        this.server.LogEntries.Should().BeEmpty();
        store.SaveCount.Should().Be(0);
        audit.Actions.Should().BeEmpty();
    }

    private FitbitOAuthOptions BuildExchangeOptions() => new()
    {
        ClientId = "test-client",
        RedirectUri = new Uri("http://127.0.0.1:7890/v1/oauth/callback"),
        TokenEndpoint = new Uri(this.server.Urls[0] + TokenPath),
    };

    private FitbitAuthorizationService BuildService(FakeTokenStore store, RecordingAuditTrail audit, FitbitOAuthOptions options)
    {
        var rng = new SequentialBytesGenerator();
        var tokenClient = new FitbitTokenClient(new HttpClient(), options, new FixedClock());
        return new FitbitAuthorizationService(
            new PkceGenerator(rng),
            new AuthorizeUrlBuilder(options),
            rng,
            tokenClient,
            store,
            audit,
            options);
    }

    private void StubTokenEndpoint()
    {
        this.server
            .Given(Request.Create().WithPath(TokenPath).UsingPost())
            .RespondWith(Response.Create()
                .WithStatusCode(200)
                .WithHeader("Content-Type", "application/json")
                .WithBody("""
                    {
                        "access_token": "new-access",
                        "refresh_token": "new-refresh",
                        "expires_in": 28800,
                        "token_type": "Bearer",
                        "scope": "heartrate sleep",
                        "user_id": "ABC123"
                    }
                    """));
    }

    public void Dispose() => this.server.Dispose();

    private sealed class FixedClock : IClock
    {
        public DateTimeOffset UtcNow { get; } = new(2024, 5, 10, 12, 0, 0, TimeSpan.Zero);
    }

    // Distinct bytes per Fill call so the PKCE draw and the state draw never coincide.
    private sealed class SequentialBytesGenerator : IRandomBytesGenerator
    {
        private byte counter;

        public void Fill(Span<byte> buffer)
        {
            for (var i = 0; i < buffer.Length; i++)
            {
                buffer[i] = (byte)(this.counter + i);
            }

            this.counter += 37;
        }
    }

    private sealed class FakeTokenStore : ITokenStore
    {
        private readonly object gate = new();

        public FakeTokenStore(OAuthToken? seed) => this.Current = seed;

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

    private sealed class RecordingAuditTrail : IAuditTrail
    {
        private readonly object gate = new();
        private readonly List<string> actions = new();

        public IReadOnlyList<string> Actions
        {
            get
            {
                lock (this.gate)
                {
                    return this.actions.ToArray();
                }
            }
        }

        public Task<AuditEntry> AppendAsync(string action, CancellationToken ct = default)
        {
            lock (this.gate)
            {
                this.actions.Add(action);
                return Task.FromResult(new AuditEntry(this.actions.Count, DateTimeOffset.UnixEpoch, action, "", ""));
            }
        }

        public Task<bool> VerifyChainAsync(CancellationToken ct = default) => Task.FromResult(true);
    }
}
