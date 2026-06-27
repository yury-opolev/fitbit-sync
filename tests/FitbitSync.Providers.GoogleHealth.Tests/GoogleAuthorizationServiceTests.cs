using FitbitSync.Domain;
using FluentAssertions;
using WireMock.RequestBuilders;
using WireMock.ResponseBuilders;
using WireMock.Server;

namespace FitbitSync.Providers.GoogleHealth.Tests;

// GoogleAuthorizationService drives the Google OAuth login through the provider-neutral
// IAuthorizationService port. Begin() composes the consent URL + anti-CSRF state; CompleteAsync()
// rejects a mismatched state BEFORE any network/persistence, and on a match exchanges the code,
// persists the token (persist-before-audit), and records exactly one "AuthGrant" audit entry.
public sealed class GoogleAuthorizationServiceTests : IDisposable
{
    private const string TokenPath = "/token";

    private readonly WireMockServer server = WireMockServer.Start();

    [Fact]
    public void Begin_ComposesAuthorizeUrl_WithStateAndOfflineAccess()
    {
        var service = BuildService(new FakeTokenStore(null), new RecordingAuditTrail(), Options());

        var session = service.Begin();

        session.AuthorizeUrl.GetLeftPart(UriPartial.Path).Should().Be("https://accounts.google.com/o/oauth2/v2/auth");
        var q = System.Web.HttpUtility.ParseQueryString(session.AuthorizeUrl.Query);
        q["state"].Should().Be(session.State);
        q["access_type"].Should().Be("offline");
        session.State.Should().NotBeNullOrEmpty();
    }

    [Fact]
    public async Task CompleteAsync_WithMatchingState_ExchangesPersistsAndAuditsAuthGrant()
    {
        StubTokenEndpoint();
        var store = new FakeTokenStore(null);
        var audit = new RecordingAuditTrail();
        var service = BuildService(store, audit, ExchangeOptions());
        var session = new AuthorizationSession(new Uri("https://accounts.google.com/o/oauth2/v2/auth"), "state-abc", "");

        var token = await service.CompleteAsync(session, returnedState: "state-abc", code: "code-1", CancellationToken.None);

        token.AccessToken.Should().Be("new-access");
        store.SaveCount.Should().Be(1);
        audit.Actions.Should().ContainSingle().Which.Should().Be("AuthGrant");

        var body = this.server.LogEntries.Single().RequestMessage.Body;
        body.Should().Contain("grant_type=authorization_code");
        body.Should().Contain("code=code-1");
    }

    [Fact]
    public async Task CompleteAsync_WithStateMismatch_Throws_AndDoesNotExchangePersistOrAudit()
    {
        StubTokenEndpoint();
        var store = new FakeTokenStore(null);
        var audit = new RecordingAuditTrail();
        var service = BuildService(store, audit, ExchangeOptions());
        var session = new AuthorizationSession(new Uri("https://accounts.google.com/o/oauth2/v2/auth"), "state-abc", "");

        var act = async () => await service.CompleteAsync(session, returnedState: "WRONG", code: "code-1", CancellationToken.None);

        await act.Should().ThrowAsync<GoogleAuthenticationException>();
        this.server.LogEntries.Should().BeEmpty();
        store.SaveCount.Should().Be(0);
        audit.Actions.Should().BeEmpty();
    }

    private static GoogleOAuthOptions Options() => new()
    {
        ClientId = "c",
        ClientSecret = "s",
        RedirectUri = new Uri("https://localhost:7654/callback"),
        Scopes = new[] { "scope1" },
    };

    private GoogleOAuthOptions ExchangeOptions() => new()
    {
        ClientId = "c",
        ClientSecret = "s",
        RedirectUri = new Uri("https://localhost:7654/callback"),
        TokenEndpoint = new Uri(this.server.Urls[0] + TokenPath),
    };

    private GoogleAuthorizationService BuildService(FakeTokenStore store, RecordingAuditTrail audit, GoogleOAuthOptions options) =>
        new(
            new GoogleAuthorizeUrlBuilder(options),
            new GoogleTokenClient(new HttpClient(), options, new FixedClock()),
            store,
            audit);

    private void StubTokenEndpoint() =>
        this.server
            .Given(Request.Create().WithPath(TokenPath).UsingPost())
            .RespondWith(Response.Create().WithStatusCode(200).WithHeader("Content-Type", "application/json").WithBody("""
                { "access_token": "new-access", "refresh_token": "new-refresh", "expires_in": 3599, "scope": "scope1", "token_type": "Bearer" }
                """));

    public void Dispose() => this.server.Dispose();

    private sealed class FixedClock : IClock
    {
        public DateTimeOffset UtcNow { get; } = new(2026, 6, 27, 12, 0, 0, TimeSpan.Zero);
    }

    private sealed class FakeTokenStore : ITokenStore
    {
        public FakeTokenStore(OAuthToken? seed) => this.Current = seed;

        public OAuthToken? Current { get; private set; }

        public int SaveCount { get; private set; }

        public Task<OAuthToken?> LoadAsync(CancellationToken ct = default) => Task.FromResult(this.Current);

        public Task SaveAsync(OAuthToken token, CancellationToken ct = default)
        {
            this.Current = token;
            this.SaveCount++;
            return Task.CompletedTask;
        }
    }

    private sealed class RecordingAuditTrail : IAuditTrail
    {
        private readonly List<string> actions = new();

        public IReadOnlyList<string> Actions => this.actions.ToArray();

        public Task<AuditEntry> AppendAsync(string action, CancellationToken ct = default)
        {
            this.actions.Add(action);
            return Task.FromResult(new AuditEntry(this.actions.Count, DateTimeOffset.UnixEpoch, action, "", ""));
        }

        public Task<bool> VerifyChainAsync(CancellationToken ct = default) => Task.FromResult(true);
    }
}
