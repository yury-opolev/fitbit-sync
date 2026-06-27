using FitbitSync.Domain;
using FluentAssertions;
using WireMock.RequestBuilders;
using WireMock.ResponseBuilders;
using WireMock.Server;

namespace FitbitSync.Providers.GoogleHealth.Tests;

// GoogleTokenClient exchanges the authorization code and refreshes tokens against the Google token
// endpoint using the confidential web client (client_secret). On a non-success response it throws a
// GoogleAuthenticationException; when a refresh response omits refresh_token, the existing one is kept.
public sealed class GoogleTokenClientTests : IDisposable
{
    private const string TokenPath = "/token";

    private readonly WireMockServer server = WireMockServer.Start();

    private GoogleOAuthOptions Options() => new()
    {
        ClientId = "client-123",
        ClientSecret = "secret-xyz",
        RedirectUri = new Uri("https://localhost:7654/callback"),
        TokenEndpoint = new Uri(this.server.Urls[0] + TokenPath),
    };

    private void StubToken(string body) =>
        this.server
            .Given(Request.Create().WithPath(TokenPath).UsingPost())
            .RespondWith(Response.Create().WithStatusCode(200).WithHeader("Content-Type", "application/json").WithBody(body));

    [Fact]
    public async Task ExchangeCodeAsync_PostsAuthorizationCodeGrant_AndParsesTokens()
    {
        StubToken("""
            { "access_token": "acc", "refresh_token": "ref", "expires_in": 3599, "scope": "a b", "token_type": "Bearer" }
            """);
        var client = new GoogleTokenClient(new HttpClient(), Options(), new FixedClock());

        var token = await client.ExchangeCodeAsync("auth-code-1", CancellationToken.None);

        token.AccessToken.Should().Be("acc");
        token.RefreshToken.Should().Be("ref");
        token.Scopes.Should().BeEquivalentTo("a", "b");

        var body = this.server.LogEntries.Single().RequestMessage.Body;
        body.Should().Contain("grant_type=authorization_code");
        body.Should().Contain("code=auth-code-1");
        body.Should().Contain("client_id=client-123");
        body.Should().Contain("client_secret=secret-xyz");
        body.Should().Contain("redirect_uri=");
    }

    [Fact]
    public async Task RefreshAsync_PostsRefreshGrant_AndKeepsRefreshTokenWhenResponseOmitsIt()
    {
        StubToken("""
            { "access_token": "acc2", "expires_in": 3599, "scope": "a", "token_type": "Bearer" }
            """);
        var client = new GoogleTokenClient(new HttpClient(), Options(), new FixedClock());

        var token = await client.RefreshAsync("old-refresh", CancellationToken.None);

        token.AccessToken.Should().Be("acc2");
        token.RefreshToken.Should().Be("old-refresh");

        var body = this.server.LogEntries.Single().RequestMessage.Body;
        body.Should().Contain("grant_type=refresh_token");
        body.Should().Contain("refresh_token=old-refresh");
    }

    [Fact]
    public async Task ExchangeCodeAsync_OnNonSuccess_ThrowsGoogleAuthenticationException()
    {
        this.server
            .Given(Request.Create().WithPath(TokenPath).UsingPost())
            .RespondWith(Response.Create().WithStatusCode(400).WithBody("{\"error\":\"invalid_grant\"}"));
        var client = new GoogleTokenClient(new HttpClient(), Options(), new FixedClock());

        var act = async () => await client.ExchangeCodeAsync("bad", CancellationToken.None);

        await act.Should().ThrowAsync<GoogleAuthenticationException>();
    }

    public void Dispose() => this.server.Dispose();

    private sealed class FixedClock : IClock
    {
        public DateTimeOffset UtcNow { get; } = new(2026, 6, 27, 12, 0, 0, TimeSpan.Zero);
    }
}
