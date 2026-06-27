using FitbitSync.Domain;
using FluentAssertions;
using WireMock.RequestBuilders;
using WireMock.ResponseBuilders;
using WireMock.Server;

namespace FitbitSync.Providers.Fitbit.Tests;

// Phase 4 (4a): FitbitTokenClient POSTs application/x-www-form-urlencoded to the OAuth token endpoint.
// RefreshAsync must send grant_type=refresh_token with the supplied refresh token and parse the rotated
// access+refresh tokens, computing ExpiresAt from expires_in via the injected IClock. When a client_secret
// is configured the request must carry HTTP Basic auth; PKCE-only puts client_id in the form body instead.
public sealed class FitbitTokenClientTests : IDisposable
{
    private const string TokenPath = "/oauth2/token";

    private readonly WireMockServer server = WireMockServer.Start();

    private sealed class FixedClock : IClock
    {
        public DateTimeOffset UtcNow { get; } = new(2024, 5, 10, 12, 0, 0, TimeSpan.Zero);
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

    private FitbitOAuthOptions BuildOptions(string? clientSecret) => new()
    {
        ClientId = "test-client",
        ClientSecret = clientSecret,
        TokenEndpoint = new Uri(this.server.Urls[0] + TokenPath),
    };

    [Fact]
    public async Task FitbitTokenClient_RefreshAsync_PostsRefreshGrant_AndParsesRotatedTokens()
    {
        // Given a token endpoint returning a rotated access+refresh pair...
        this.StubTokenEndpoint();
        var clock = new FixedClock();
        var client = new FitbitTokenClient(new HttpClient(), this.BuildOptions(clientSecret: null), clock);

        // When the stored refresh token is exchanged...
        var token = await client.RefreshAsync("old-refresh", CancellationToken.None);

        // Then the rotated tokens are parsed and ExpiresAt is computed from expires_in via the clock.
        token.AccessToken.Should().Be("new-access");
        token.RefreshToken.Should().Be("new-refresh");
        token.ExpiresAt.Should().Be(clock.UtcNow.AddSeconds(28800));
        token.Scopes.Should().BeEquivalentTo("heartrate", "sleep");

        // ...and the request used grant_type=refresh_token with the supplied refresh token in the form body.
        var body = this.server.LogEntries.Single().RequestMessage.Body;
        body.Should().Contain("grant_type=refresh_token");
        body.Should().Contain("refresh_token=old-refresh");
    }

    [Fact]
    public async Task FitbitTokenClient_WithClientSecret_SendsBasicAuthHeader()
    {
        // Given a configured client_secret...
        this.StubTokenEndpoint();
        var client = new FitbitTokenClient(new HttpClient(), this.BuildOptions(clientSecret: "shh"), new FixedClock());

        // When a refresh is performed...
        await client.RefreshAsync("old-refresh", CancellationToken.None);

        // Then the request authenticates with HTTP Basic instead of a form-body client_id.
        var headers = this.server.LogEntries.Single().RequestMessage.Headers;
        headers.Should().ContainKey("Authorization");
        headers!["Authorization"].Single().Should().StartWith("Basic ");
    }

    public void Dispose() => this.server.Dispose();
}
