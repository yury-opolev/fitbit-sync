using System.Text;
using FitbitSync.Providers.Fitbit;
using FluentAssertions;
using Xunit;

namespace FitbitSync.Providers.Fitbit.Tests;

public sealed class PkceAndAuthorizeUrlTests
{
    // RFC 7636 Appendix B official test vector: this exact 32-byte octet sequence MUST yield
    // verifier "dBjftJeZ4CVP-mB92K27uhbUJU1p1r_wW1gFWFOEjXk" and S256 challenge
    // "E9Melhoa2OwvFrEMTJguCHaoeK1t8URWbuGJSstw-cM".
    private static readonly byte[] Rfc7636Octets =
    {
        116, 24, 223, 180, 151, 153, 224, 37, 79, 250, 96, 125, 216, 173,
        187, 186, 22, 212, 37, 77, 105, 214, 191, 240, 91, 88, 5, 88, 83,
        132, 141, 121,
    };

    private sealed class FixedBytesGenerator : IRandomBytesGenerator
    {
        private readonly byte[] bytes;
        public FixedBytesGenerator(byte[] bytes) => this.bytes = bytes;
        public void Fill(Span<byte> buffer) => this.bytes.AsSpan(0, buffer.Length).CopyTo(buffer);
    }

    [Fact]
    public void Generate_WithRfc7636Vector_ProducesPublishedVerifierAndChallenge()
    {
        var generator = new PkceGenerator(new FixedBytesGenerator(Rfc7636Octets));

        var codes = generator.Generate();

        codes.Verifier.Should().Be("dBjftJeZ4CVP-mB92K27uhbUJU1p1r_wW1gFWFOEjXk");
        codes.Challenge.Should().Be("E9Melhoa2OwvFrEMTJguCHaoeK1t8URWbuGJSstw-cM");
    }

    [Fact]
    public void Generate_VerifierIsBase64UrlWithoutPadding_AndInLengthRange()
    {
        var generator = new PkceGenerator(new FixedBytesGenerator(Rfc7636Octets));

        var verifier = generator.Generate().Verifier;

        verifier.Should().NotContain("=").And.NotContain("+").And.NotContain("/");
        verifier.Length.Should().BeInRange(43, 128);
    }

    [Fact]
    public void Build_ComposesAuthorizeUrl_WithAllRequiredQueryParams()
    {
        var options = new FitbitOAuthOptions
        {
            ClientId = "ABC123",
            RedirectUri = new Uri("http://127.0.0.1:7890/v1/oauth/callback"),
            Scopes = new[] { "heartrate", "sleep" },
        };
        var builder = new AuthorizeUrlBuilder(options);

        var url = builder.Build("CHALLENGE_XYZ", "state-123");

        url.GetLeftPart(UriPartial.Path).Should().Be("https://www.fitbit.com/oauth2/authorize");
        var q = System.Web.HttpUtility.ParseQueryString(url.Query);
        q["response_type"].Should().Be("code");
        q["client_id"].Should().Be("ABC123");
        q["redirect_uri"].Should().Be("http://127.0.0.1:7890/v1/oauth/callback");
        q["scope"].Should().Be("heartrate sleep");
        q["code_challenge"].Should().Be("CHALLENGE_XYZ");
        q["code_challenge_method"].Should().Be("S256");
        q["state"].Should().Be("state-123");
    }

    [Fact]
    public void Build_Throws_WhenRedirectUriMissing()
    {
        var options = new FitbitOAuthOptions { ClientId = "ABC123" };
        var builder = new AuthorizeUrlBuilder(options);

        var act = () => builder.Build("CHALLENGE_XYZ", "state-123");

        act.Should().Throw<InvalidOperationException>();
    }
}
