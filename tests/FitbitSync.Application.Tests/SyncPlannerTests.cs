using FitbitSync.Domain;

namespace FitbitSync.Application.Tests;

// Phase 5 (5e): the planner turns capabilities + durable checkpoints into an ordered list of one-day
// work items. Freshness-first (§5.6): EVERY incremental item is planned before ANY backfill item, so a
// constrained budget always spends on current data first and backfill consumes only the remainder.
// Incremental walks forward from the day after NewestSynced up to today; backfill walks backward one day
// at a time from the oldest known day toward a floor (today - BackfillWindow). Pure logic, no I/O.
public sealed class SyncPlannerTests
{
    private static readonly DateOnly Today = new(2024, 5, 10);

    private static SyncPlanner Build(int backfillDays = 30) =>
        new(new SyncOptions { BackfillWindow = TimeSpan.FromDays(backfillDays) });

    private static IReadOnlyDictionary<MetricType, SyncCheckpoint?> Checkpoints(params (MetricType Metric, SyncCheckpoint? Checkpoint)[] entries) =>
        entries.ToDictionary(entry => entry.Metric, entry => entry.Checkpoint);

    private static DateTimeOffset Midnight(DateOnly date) => new(date.ToDateTime(TimeOnly.MinValue), TimeSpan.Zero);

    [Fact]
    public void Plan_ColdStart_FetchesToday_AndBackfillsWindow()
    {
        var planner = Build(backfillDays: 3);
        var capabilities = new List<MetricCapability> { new(MetricType.SpO2, IntradayResolution.Daily) };

        var plan = planner.PlanScheduledWork(capabilities, Checkpoints((MetricType.SpO2, null)), Today);

        // Incremental: with no checkpoint we fetch today once.
        plan.Where(item => item.Kind == SyncWorkKind.Incremental).Select(item => item.Date)
            .Should().Equal(Today);

        // Backfill: yesterday back to the floor (today - 3 days), descending.
        plan.Where(item => item.Kind == SyncWorkKind.Backfill).Select(item => item.Date)
            .Should().Equal(
                new DateOnly(2024, 5, 9),
                new DateOnly(2024, 5, 8),
                new DateOnly(2024, 5, 7));
    }

    [Fact]
    public void Plan_OrdersAllIncremental_BeforeAnyBackfill()
    {
        var planner = Build(backfillDays: 2);
        var capabilities = new List<MetricCapability>
        {
            new(MetricType.HeartRate, IntradayResolution.OneMinute),
            new(MetricType.SpO2, IntradayResolution.Daily),
        };

        var plan = planner.PlanScheduledWork(
            capabilities,
            Checkpoints((MetricType.HeartRate, null), (MetricType.SpO2, null)),
            Today);

        var firstBackfillIndex = plan.ToList().FindIndex(item => item.Kind == SyncWorkKind.Backfill);
        var lastIncrementalIndex = plan.ToList().FindLastIndex(item => item.Kind == SyncWorkKind.Incremental);

        // Freshness-first: no backfill item appears before the last incremental item.
        firstBackfillIndex.Should().BeGreaterThan(lastIncrementalIndex);
    }

    [Fact]
    public void Plan_Incremental_WalksForward_FromDayAfterNewest_ToToday()
    {
        var planner = Build();
        var checkpoint = new SyncCheckpoint(MetricType.SpO2, Midnight(new DateOnly(2024, 5, 7)), null);
        var capabilities = new List<MetricCapability> { new(MetricType.SpO2, IntradayResolution.Daily) };

        var plan = planner.PlanScheduledWork(capabilities, Checkpoints((MetricType.SpO2, checkpoint)), Today);

        plan.Where(item => item.Kind == SyncWorkKind.Incremental).Select(item => item.Date)
            .Should().Equal(
                new DateOnly(2024, 5, 8),
                new DateOnly(2024, 5, 9),
                new DateOnly(2024, 5, 10));
    }

    [Fact]
    public void Plan_NoIncremental_WhenAlreadyCurrent()
    {
        var planner = Build(backfillDays: 0);
        var checkpoint = new SyncCheckpoint(MetricType.SpO2, Midnight(Today), Midnight(Today));
        var capabilities = new List<MetricCapability> { new(MetricType.SpO2, IntradayResolution.Daily) };

        var plan = planner.PlanScheduledWork(capabilities, Checkpoints((MetricType.SpO2, checkpoint)), Today);

        plan.Should().BeEmpty();
    }

    [Fact]
    public void Plan_Backfill_StopsAtFloor()
    {
        var planner = Build(backfillDays: 5);
        // Oldest backfilled is 2 days ago; backfill should continue from 3 days ago down to the floor.
        var checkpoint = new SyncCheckpoint(MetricType.SpO2, Midnight(Today), Midnight(new DateOnly(2024, 5, 8)));
        var capabilities = new List<MetricCapability> { new(MetricType.SpO2, IntradayResolution.Daily) };

        var plan = planner.PlanScheduledWork(capabilities, Checkpoints((MetricType.SpO2, checkpoint)), Today);

        plan.Where(item => item.Kind == SyncWorkKind.Backfill).Select(item => item.Date)
            .Should().Equal(
                new DateOnly(2024, 5, 7),
                new DateOnly(2024, 5, 6),
                new DateOnly(2024, 5, 5));
    }
}
