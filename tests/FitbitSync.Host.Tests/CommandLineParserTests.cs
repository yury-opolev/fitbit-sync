using FluentAssertions;

namespace FitbitSync.Host.Tests;

// Pure verb-dispatch parsing. The browser/socket shell is untested; this is the testable core.
public sealed class CommandLineParserTests
{
    [Theory]
    [InlineData("login", CliVerb.Login)]
    [InlineData("LOGIN", CliVerb.Login)]
    [InlineData("run", CliVerb.Run)]
    [InlineData("Run", CliVerb.Run)]
    [InlineData("verify", CliVerb.Verify)]
    [InlineData("VERIFY", CliVerb.Verify)]
    [InlineData("rotate-keys", CliVerb.RotateKeys)]
    [InlineData("Rotate-Keys", CliVerb.RotateKeys)]
    [InlineData("help", CliVerb.Help)]
    [InlineData("--help", CliVerb.Help)]
    [InlineData("-h", CliVerb.Help)]
    public void Parse_RecognizesKnownVerbs(string arg, CliVerb expected)
    {
        var result = CommandLineParser.Parse([arg]);

        result.Verb.Should().Be(expected);
        result.Error.Should().BeNull();
    }

    [Fact]
    public void Parse_NoArgs_DefaultsToHelp()
    {
        var result = CommandLineParser.Parse([]);

        result.Verb.Should().Be(CliVerb.Help);
        result.Error.Should().BeNull();
    }

    [Fact]
    public void Parse_UnknownVerb_ReturnsErrorAndNoneVerb()
    {
        var result = CommandLineParser.Parse(["frobnicate"]);

        result.Verb.Should().Be(CliVerb.None);
        result.Error.Should().Contain("frobnicate");
        result.IsValid.Should().BeFalse();
    }

    [Fact]
    public void Parse_TrimsSurroundingWhitespace()
    {
        var result = CommandLineParser.Parse(["  run  "]);

        result.Verb.Should().Be(CliVerb.Run);
    }

    [Fact]
    public void IsValid_IsTrue_OnlyForActionableVerbs()
    {
        CommandLineParser.Parse(["login"]).IsValid.Should().BeTrue();
        CommandLineParser.Parse(["help"]).IsValid.Should().BeTrue();
        new ParsedCliCommand(CliVerb.None).IsValid.Should().BeFalse();
    }

    [Fact]
    public void UsageText_DocumentsVerifyVerb()
    {
        CommandLineParser.UsageText.Should().Contain("verify");
    }

    [Fact]
    public void UsageText_DocumentsRotateKeysVerb()
    {
        CommandLineParser.UsageText.Should().Contain("rotate-keys");
    }
}
