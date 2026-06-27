using FitbitSync.Domain;
using FluentAssertions;

namespace FitbitSync.Host.Tests;

// Phase 8: agent verbs carry flags (--from/--to/--metric/--coverage). CommandLineParser is the testable
// core that turns "verb + flags" into a ParsedCliCommand with typed CliOptions OR a fail-fast usage error
// (which the host maps to exit code 1 + a JSON error). Date parsing is strict ISO yyyy-MM-dd; metric names
// are case-insensitive enum names.
public sealed class CommandLineParserAgentVerbsTests
{
    [Theory]
    [InlineData("sync-once", CliVerb.SyncOnce)]
    [InlineData("SYNC-ONCE", CliVerb.SyncOnce)]
    [InlineData("backfill", CliVerb.Backfill)]
    [InlineData("query", CliVerb.Query)]
    public void Parse_RecognizesAgentVerbs(string arg, CliVerb expected)
    {
        var result = CommandLineParser.Parse([arg]);

        result.Verb.Should().Be(expected);
        result.Error.Should().BeNull();
    }

    [Fact]
    public void Parse_SyncOnce_HasNoOptions_ButIsValid()
    {
        var result = CommandLineParser.Parse(["sync-once"]);

        result.IsValid.Should().BeTrue();
        result.Options.Should().NotBeNull();
    }

    [Fact]
    public void Parse_Backfill_ParsesFromToAndMetric()
    {
        var result = CommandLineParser.Parse(["backfill", "--from", "2024-01-01", "--to", "2024-01-31", "--metric", "heartRate"]);

        result.Error.Should().BeNull();
        result.Options!.From.Should().Be(new DateOnly(2024, 1, 1));
        result.Options.To.Should().Be(new DateOnly(2024, 1, 31));
        result.Options.Metric.Should().Be(MetricType.HeartRate);
    }

    [Fact]
    public void Parse_Metric_IsCaseInsensitive()
    {
        var result = CommandLineParser.Parse(["query", "--metric", "spo2", "--from", "2024-01-01", "--to", "2024-01-01"]);

        result.Error.Should().BeNull();
        result.Options!.Metric.Should().Be(MetricType.SpO2);
    }

    [Fact]
    public void Parse_Query_Coverage_SetsCoverageFlag()
    {
        var result = CommandLineParser.Parse(["query", "--coverage"]);

        result.Error.Should().BeNull();
        result.Options!.Coverage.Should().BeTrue();
    }

    [Fact]
    public void Parse_InvalidFromDate_ReturnsError()
    {
        var result = CommandLineParser.Parse(["backfill", "--from", "01/01/2024", "--to", "2024-01-31"]);

        result.IsValid.Should().BeFalse();
        result.Error.Should().Contain("--from");
    }

    [Fact]
    public void Parse_UnknownOption_ReturnsError()
    {
        var result = CommandLineParser.Parse(["query", "--bogus"]);

        result.IsValid.Should().BeFalse();
        result.Error.Should().Contain("--bogus");
    }

    [Fact]
    public void Parse_UnknownMetric_ReturnsError()
    {
        var result = CommandLineParser.Parse(["query", "--metric", "calories"]);

        result.IsValid.Should().BeFalse();
        result.Error.Should().Contain("calories");
    }

    [Fact]
    public void Parse_FlagMissingValue_ReturnsError()
    {
        var result = CommandLineParser.Parse(["backfill", "--from"]);

        result.IsValid.Should().BeFalse();
        result.Error.Should().Contain("--from");
    }

    [Fact]
    public void Parse_FlagFollowedByAnotherFlag_TreatedAsMissingValue()
    {
        var result = CommandLineParser.Parse(["backfill", "--from", "--to", "2024-01-31"]);

        result.IsValid.Should().BeFalse();
        result.Error.Should().Contain("--from");
    }

    [Fact]
    public void UsageText_DocumentsAgentVerbs()
    {
        CommandLineParser.UsageText.Should().Contain("sync-once");
        CommandLineParser.UsageText.Should().Contain("backfill");
        CommandLineParser.UsageText.Should().Contain("query");
    }
}
