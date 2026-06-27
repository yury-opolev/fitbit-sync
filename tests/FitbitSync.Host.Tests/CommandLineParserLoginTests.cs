using FluentAssertions;

namespace FitbitSync.Host.Tests;

// Headless login parsing: `login` stays the interactive desktop flow; `login --begin` and
// `login --complete --redirect <url>` are the agent-driven, JSON-emitting modes. Parse errors for the
// headless modes still carry the detected LoginMode so the host can emit a JSON error envelope.
public sealed class CommandLineParserLoginTests
{
    private const string CallbackUrl = "http://127.0.0.1:7654/callback?code=abc&state=xyz";

    [Fact]
    public void Login_NoFlags_IsInteractiveMode()
    {
        var result = CommandLineParser.Parse(["login"]);

        result.Verb.Should().Be(CliVerb.Login);
        result.Error.Should().BeNull();
        result.Options!.LoginMode.Should().Be(LoginMode.Interactive);
    }

    [Fact]
    public void Login_Begin_ParsesBeginMode()
    {
        var result = CommandLineParser.Parse(["login", "--begin"]);

        result.Verb.Should().Be(CliVerb.Login);
        result.Error.Should().BeNull();
        result.Options!.LoginMode.Should().Be(LoginMode.Begin);
    }

    [Fact]
    public void Login_CompleteWithRedirect_ParsesCompleteModeAndRedirect()
    {
        var result = CommandLineParser.Parse(["login", "--complete", "--redirect", CallbackUrl]);

        result.Verb.Should().Be(CliVerb.Login);
        result.Error.Should().BeNull();
        result.Options!.LoginMode.Should().Be(LoginMode.Complete);
        result.Options.Redirect.Should().Be(CallbackUrl);
    }

    [Fact]
    public void Login_CompleteWithoutRedirect_IsErrorButStaysCompleteMode()
    {
        var result = CommandLineParser.Parse(["login", "--complete"]);

        result.Error.Should().NotBeNull();
        result.Error.Should().Contain("redirect");
        result.Options!.LoginMode.Should().Be(LoginMode.Complete);
    }

    [Fact]
    public void Login_RedirectMissingValue_IsError()
    {
        var result = CommandLineParser.Parse(["login", "--complete", "--redirect"]);

        result.Error.Should().NotBeNull();
        result.Error.Should().Contain("redirect");
    }

    [Fact]
    public void Login_BeginAndComplete_AreMutuallyExclusive()
    {
        var result = CommandLineParser.Parse(["login", "--begin", "--complete"]);

        result.Error.Should().NotBeNull();
        result.Error.Should().Contain("mutually exclusive");
    }

    [Fact]
    public void Login_RedirectWithoutComplete_IsError()
    {
        var result = CommandLineParser.Parse(["login", "--redirect", CallbackUrl]);

        result.Error.Should().NotBeNull();
        result.Error.Should().Contain("--complete");
    }

    [Fact]
    public void Login_UnknownOption_IsError()
    {
        var result = CommandLineParser.Parse(["login", "--bogus"]);

        result.Error.Should().NotBeNull();
        result.Error.Should().Contain("bogus");
    }

    [Fact]
    public void UsageText_DocumentsHeadlessLoginVerbs()
    {
        CommandLineParser.UsageText.Should().Contain("--begin");
        CommandLineParser.UsageText.Should().Contain("--complete");
    }
}
