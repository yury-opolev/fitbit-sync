using System.Web;
using FluentAssertions;

namespace FitbitSync.Providers.GoogleHealth.Tests;

// The Google authorize URL must request offline access + forced consent (so a refresh token is issued),
// carry the anti-CSRF state, and the requested scopes — and must NOT set include_granted_scopes (which
// would merge unrelated Gmail scopes that the Health API then rejects).
public sealed class GoogleAuthorizeUrlBuilderTests
{
    private static GoogleOAuthOptions Options() => new()
    {
        ClientId = "client-123.apps.googleusercontent.com",
        ClientSecret = "secret",
        RedirectUri = new Uri("https://localhost:7654/callback"),
        Scopes = new[]
        {
            "https://www.googleapis.com/auth/googlehealth.activity_and_fitness.readonly",
            "https://www.googleapis.com/auth/googlehealth.sleep.readonly",
        },
    };

    [Fact]
    public void Build_ComposesGoogleAuthorizeUrl_WithOfflineConsentStateAndScopes()
    {
        var url = new GoogleAuthorizeUrlBuilder(Options()).Build("state-xyz");

        url.GetLeftPart(UriPartial.Path).Should().Be("https://accounts.google.com/o/oauth2/v2/auth");
        var q = HttpUtility.ParseQueryString(url.Query);
        q["response_type"].Should().Be("code");
        q["client_id"].Should().Be("client-123.apps.googleusercontent.com");
        q["redirect_uri"].Should().Be("https://localhost:7654/callback");
        q["access_type"].Should().Be("offline");
        q["prompt"].Should().Be("consent");
        q["state"].Should().Be("state-xyz");
        q["scope"].Should().Contain("googlehealth.activity_and_fitness.readonly");
        q["scope"].Should().Contain("googlehealth.sleep.readonly");
    }

    [Fact]
    public void Build_DoesNotRequestIncludeGrantedScopes()
    {
        var url = new GoogleAuthorizeUrlBuilder(Options()).Build("s");

        HttpUtility.ParseQueryString(url.Query)["include_granted_scopes"].Should().BeNull();
    }
}
