using FitbitSync.Domain;

namespace FitbitSync.Application.Tests;

// Phase 5 (5i): RunForceSyncAsync executes a TARGETED plan. With no filter it syncs every capability for
// today; with a metric filter only those metrics; with a date range every day in the range. It reuses the
// shared gate and checkpoints (so it can't exceed budget) and writes a "ForceSync" audit entry. The
// command's run id is echoed on the result for status polling.
public sealed class SyncEngineForceSyncTests
{
    private static readonly DateTimeOffset Start = new(2024, 5, 10, 9, 0, 0, TimeSpan.Zero);

    private static readonly IReadOnlyList<MetricCapability> TwoMetrics =
    [
        new MetricCapability(MetricType.SpO2, IntradayResolution.Daily),
        new MetricCapability(MetricType.HeartRate, IntradayResolution.OneMinute),
    ];

    private static (SyncEngine Engine, RecordingHealthDataProvider Provider, InMemoryAuditTrail Audit)
        Build(TestClock clock)
    {
        var provider = new RecordingHealthDataProvider(TwoMetrics);
        var options = new SyncOptions { RetryBaseDelay = TimeSpan.Zero };
        var audit = new InMemoryAuditTrail();
        var engine = new SyncEngine(
            provider,
            new RecordingMetricRepository(),
            new InMemoryCheckpointStore(),
            audit,
            new TokenBucketRateLimitGate(clock, options),
            new SyncPlanner(options),
            new SyncResiliencePipelineProvider(options),
            clock);
        return (engine, provider, audit);
    }

    [Fact]
    public async Task RunForceSync_AllMetrics_SyncsTodayForEveryCapability_AndAuditsForceSync()
    {
        var clock = new TestClock(Start);
        var (engine, provider, audit) = Build(clock);
        var command = ForceSyncCommand.ForAll();

        var run = await engine.RunForceSyncAsync(command);

        run.RunId.Should().Be(command.RunId);
        run.Outcome.Should().Be(SyncRunOutcome.Completed);
        // One day (today) per capability.
        provider.FetchedDates.Should().HaveCount(2);
        provider.FetchedDates.Should().AllBeEquivalentTo(new DateOnly(2024, 5, 10));
        audit.Actions.Should().Contain("ForceSync");
    }

    [Fact]
    public async Task RunForceSync_MetricFilter_OnlySyncsRequestedMetric()
    {
        var clock = new TestClock(Start);
        var (engine, provider, _) = Build(clock);

        var run = await engine.RunForceSyncAsync(ForceSyncCommand.For([MetricType.SpO2]));

        run.ItemsCompleted.Should().Be(1);
        provider.FetchedDates.Should().ContainSingle();
    }

    [Fact]
    public async Task RunForceSync_DateRange_SyncsEveryDayInRange()
    {
        var clock = new TestClock(Start);
        var (engine, provider, _) = Build(clock);
        var range = new DateRange(new DateOnly(2024, 5, 1), new DateOnly(2024, 5, 3));

        var run = await engine.RunForceSyncAsync(ForceSyncCommand.For([MetricType.SpO2], range));

        run.ItemsCompleted.Should().Be(3);
        provider.FetchedDates.Should().Equal(
            new DateOnly(2024, 5, 1),
            new DateOnly(2024, 5, 2),
            new DateOnly(2024, 5, 3));
    }
}
