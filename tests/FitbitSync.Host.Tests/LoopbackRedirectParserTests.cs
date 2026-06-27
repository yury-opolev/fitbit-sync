using FluentAssertions;

namespace FitbitSync.Host.Tests;

// Pure redirect-query parsing: the anti-CSRF state check lives in OAuthStateValidator (6d);
// here we only translate the URL into a structured result.
public sealed class LoopbackRedirectParserTests
{
    [Fact]
    public void Parse_SuccessRedirect_ExtractsCodeAndState()
    {
        var redirect = new Uri("http://127.0.0.1:7654/callback?code=abc123&state=xyz789");

        var result = LoopbackRedirectParser.Parse(redirect);

        result.IsSuccess.Should().BeTrue();
        result.Code.Should().Be("abc123");
        result.State.Should().Be("xyz789");
        result.Error.Should().BeNull();
    }

    [Fact]
    public void Parse_UrlEncodedValues_AreDecoded()
    {
        var redirect = new Uri("http://127.0.0.1:7654/callback?code=a%2Bb%2Fc&state=s%3Dt");

        var result = LoopbackRedirectParser.Parse(redirect);

        result.Code.Should().Be("a+b/c");
        result.State.Should().Be("s=t");
    }

    [Fact]
    public void Parse_ErrorRedirect_ReturnsFailureWithDescription()
    {
        var redirect = new Uri("http://127.0.0.1:7654/callback?error=access_denied&error_description=User%20declined");

        var result = LoopbackRedirectParser.Parse(redirect);

        result.IsSuccess.Should().BeFalse();
        result.Error.Should().Be("access_denied: User declined");
        result.Code.Should().BeNull();
    }

    [Fact]
    public void Parse_ErrorWithoutDescription_UsesErrorCode()
    {
        var redirect = new Uri("http://127.0.0.1:7654/callback?error=invalid_scope");

        var result = LoopbackRedirectParser.Parse(redirect);

        result.Error.Should().Be("invalid_scope");
    }

    [Fact]
    public void Parse_MissingState_ReturnsFailure()
    {
        var redirect = new Uri("http://127.0.0.1:7654/callback?code=abc123");

        var result = LoopbackRedirectParser.Parse(redirect);

        result.IsSuccess.Should().BeFalse();
        result.Error.Should().Contain("state");
    }

    [Fact]
    public void Parse_MissingCode_ReturnsFailure()
    {
        var redirect = new Uri("http://127.0.0.1:7654/callback?state=xyz789");

        var result = LoopbackRedirectParser.Parse(redirect);

        result.IsSuccess.Should().BeFalse();
        result.Error.Should().Contain("code");
    }

    [Fact]
    public void Parse_EmptyQuery_ReturnsFailure()
    {
        var redirect = new Uri("http://127.0.0.1:7654/callback");

        var result = LoopbackRedirectParser.Parse(redirect);

        result.IsSuccess.Should().BeFalse();
    }
}
