using FitbitSync.Domain;
using FluentAssertions;

namespace FitbitSync.Host.Tests;

// Phase 8: AgentArguments is the fail-fast validation the backfill/query shells apply to parsed flags before
// touching the Application core: a range requires both --from and --to with from<=to, and query's sample
// mode requires --metric. Failures throw AgentCommandException (code "usage"), which the host renders as an
// exit-1 JSON error envelope. Pure and directly unit-tested.
public sealed class AgentArgumentsTests
{
    [Fact]
    public void RequireRange_ReturnsRange_WhenFromBeforeTo()
    {
        var options = new CliOptions { From = new DateOnly(2024, 1, 1), To = new DateOnly(2024, 1, 31) };

        var range = AgentArguments.RequireRange(options);

        range.Start.Should().Be(new DateOnly(2024, 1, 1));
        range.End.Should().Be(new DateOnly(2024, 1, 31));
    }

    [Fact]
    public void RequireRange_AllowsSingleDay_FromEqualsTo()
    {
        var options = new CliOptions { From = new DateOnly(2024, 1, 5), To = new DateOnly(2024, 1, 5) };

        var range = AgentArguments.RequireRange(options);

        range.Start.Should().Be(range.End);
    }

    [Fact]
    public void RequireRange_MissingFrom_ThrowsUsage()
    {
        var options = new CliOptions { To = new DateOnly(2024, 1, 31) };

        var act = () => AgentArguments.RequireRange(options);

        act.Should().Throw<AgentCommandException>()
            .Where(ex => ex.Code == "usage")
            .WithMessage("*--from*");
    }

    [Fact]
    public void RequireRange_MissingTo_ThrowsUsage()
    {
        var options = new CliOptions { From = new DateOnly(2024, 1, 1) };

        var act = () => AgentArguments.RequireRange(options);

        act.Should().Throw<AgentCommandException>().WithMessage("*--to*");
    }

    [Fact]
    public void RequireRange_FromAfterTo_ThrowsUsage()
    {
        var options = new CliOptions { From = new DateOnly(2024, 2, 1), To = new DateOnly(2024, 1, 1) };

        var act = () => AgentArguments.RequireRange(options);

        act.Should().Throw<AgentCommandException>()
            .Where(ex => ex.Code == "usage")
            .WithMessage("*must be on or before*");
    }

    [Fact]
    public void RequireMetric_ReturnsMetric_WhenPresent()
    {
        AgentArguments.RequireMetric(new CliOptions { Metric = MetricType.Sleep }).Should().Be(MetricType.Sleep);
    }

    [Fact]
    public void RequireMetric_Missing_ThrowsUsage()
    {
        var act = () => AgentArguments.RequireMetric(new CliOptions());

        act.Should().Throw<AgentCommandException>().Where(ex => ex.Code == "usage").WithMessage("*--metric*");
    }
}
