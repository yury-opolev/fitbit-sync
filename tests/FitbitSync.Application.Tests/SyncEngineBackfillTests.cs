using FitbitSync.Domain;

namespace FitbitSync.Application.Tests;

// Phase 8: gap-aware idempotent backfill. RunBackfillAsync derives coverage from what is ACTUALLY stored
// (RecordingMetricRepository.GetCoveredDatesAsync), computes the missing dates, and fetches ONLY those.
// The headline guarantee from the scope refinement: a backfill over a FULLY-COVERED range makes ZERO
// provider calls (asserted via provider.FetchedDates). An interior gap is fetched; a rate-limit stop
// records the unfetched dates as stillMissing. The shared gate/checkpoints/audit are reused.
public sealed class SyncEngineBackfillTests
{
    private static readonly DateTimeOffset Start = new(2024, 5, 10, 9, 0, 0, TimeSpan.Zero);
    private static readonly DateOnly Jan1 = new(2024, 1, 1);

    private static readonly IReadOnlyList<MetricCapability> OneDailyMetric =
        [new MetricCapability(MetricType.HeartRate, IntradayResolution.Daily)];

    private static MetricSample HeartRateOn(DateOnly date) =>
        new(MetricType.HeartRate, new DateTimeOffset(date.ToDateTime(TimeOnly.MinValue), TimeSpan.Zero), 60, "bpm", IntradayResolution.Daily, "fitbit");

    private static (SyncEngine Engine, RecordingHealthDataProvider Provider, RecordingMetricRepository Repository, InMemoryAuditTrail Audit)
        Build(TestClock clock, RecordingHealthDataProvider provider, RecordingMetricRepository repository, int budget = 150)
    {
        var options = new SyncOptions { RetryBaseDelay = TimeSpan.Zero, HourlyRequestBudget = budget };
        var audit = new InMemoryAuditTrail();
        var engine = new SyncEngine(
            provider,
            repository,
            new InMemoryCheckpointStore(),
            audit,
            new TokenBucketRateLimitGate(clock, options),
            new SyncPlanner(options),
            new SyncResiliencePipelineProvider(options),
            clock);
        return (engine, provider, repository, audit);
    }

    [Fact]
    public async Task RunBackfill_OverFullyCoveredRange_MakesZeroProviderCalls()
    {
        // Given the repository already holds every day in the requested range...
        var range = new DateRange(Jan1, Jan1.AddDays(2));
        var repository = new RecordingMetricRepository();
        repository.Upserted.AddRange([HeartRateOn(Jan1), HeartRateOn(Jan1.AddDays(1)), HeartRateOn(Jan1.AddDays(2))]);
        var clock = new TestClock(Start);
        var provider = new RecordingHealthDataProvider(OneDailyMetric);
        var (engine, _, _, _) = Build(clock, provider, repository);

        // When backfill runs over that fully-covered range...
        var result = await engine.RunBackfillAsync(BackfillCommand.For(range));

        // Then NOT a single Fitbit fetch happens — covered dates cost zero API round-trips.
        provider.FetchedDates.Should().BeEmpty();
        result.Outcome.Should().Be(SyncRunOutcome.Completed);
        var report = result.Metrics.Single();
        report.AlreadyCovered.Should().HaveCount(3);
        report.Fetched.Should().BeEmpty();
        report.StillMissing.Should().BeEmpty();
        report.SamplesWritten.Should().Be(0);
    }

    [Fact]
    public async Task RunBackfill_FetchesOnlyTheInteriorGap()
    {
        // Given the endpoints are held but the middle day (Jan 2) is missing...
        var range = new DateRange(Jan1, Jan1.AddDays(2));
        var repository = new RecordingMetricRepository();
        repository.Upserted.AddRange([HeartRateOn(Jan1), HeartRateOn(Jan1.AddDays(2))]);
        var clock = new TestClock(Start);
        var provider = new RecordingHealthDataProvider(OneDailyMetric);
        var (engine, _, _, _) = Build(clock, provider, repository);

        var result = await engine.RunBackfillAsync(BackfillCommand.For(range));

        // Then ONLY the gap day is fetched.
        provider.FetchedDates.Should().Equal(Jan1.AddDays(1));
        var report = result.Metrics.Single();
        report.AlreadyCovered.Should().BeEquivalentTo([Jan1, Jan1.AddDays(2)]);
        report.Fetched.Should().Equal(Jan1.AddDays(1));
        report.StillMissing.Should().BeEmpty();
    }

    [Fact]
    public async Task RunBackfill_EmptyStore_FetchesEveryDayInRange()
    {
        var range = new DateRange(Jan1, Jan1.AddDays(2));
        var clock = new TestClock(Start);
        var provider = new RecordingHealthDataProvider(OneDailyMetric);
        var (engine, _, repository, audit) = Build(clock, provider, new RecordingMetricRepository());

        var result = await engine.RunBackfillAsync(BackfillCommand.For(range));

        provider.FetchedDates.Should().Equal(Jan1, Jan1.AddDays(1), Jan1.AddDays(2));
        result.Metrics.Single().Fetched.Should().HaveCount(3);
        repository.Upserted.Should().HaveCount(3);
        audit.Actions.Should().Contain("Backfill");
    }

    [Fact]
    public async Task RunBackfill_WhenBudgetExhausted_RecordsAllGapsAsStillMissing_AndRateLimited()
    {
        var range = new DateRange(Jan1, Jan1.AddDays(2));
        var clock = new TestClock(Start);
        var provider = new RecordingHealthDataProvider(OneDailyMetric);
        var (engine, _, _, _) = Build(clock, provider, new RecordingMetricRepository(), budget: 0);

        var result = await engine.RunBackfillAsync(BackfillCommand.For(range));

        result.Outcome.Should().Be(SyncRunOutcome.RateLimited);
        provider.FetchedDates.Should().BeEmpty();
        var report = result.Metrics.Single();
        report.Fetched.Should().BeEmpty();
        report.StillMissing.Should().HaveCount(3);
    }

    [Fact]
    public async Task RunBackfill_On429MidRun_StopsAndRecordsRemainingAsStillMissing()
    {
        // Budget allows one fetch; the first 429s, so the gate pauses and the remaining gap days are unfetched.
        var range = new DateRange(Jan1, Jan1.AddDays(2));
        var clock = new TestClock(Start);
        var provider = new RecordingHealthDataProvider(OneDailyMetric)
        {
            FetchOverride = _ => throw new ProviderRateLimitedException(new RateLimitSnapshot(0, 150, 900, clock.UtcNow)),
        };
        var (engine, _, _, audit) = Build(clock, provider, new RecordingMetricRepository());

        var result = await engine.RunBackfillAsync(BackfillCommand.For(range));

        result.Outcome.Should().Be(SyncRunOutcome.RateLimited);
        var report = result.Metrics.Single();
        report.Fetched.Should().BeEmpty();
        report.StillMissing.Should().HaveCount(3);
        audit.Actions.Should().Contain("RateLimited");
    }

    [Fact]
    public async Task RunBackfill_MetricFilter_OnlyBackfillsRequestedMetric()
    {
        var twoMetrics = new List<MetricCapability>
        {
            new(MetricType.HeartRate, IntradayResolution.Daily),
            new(MetricType.SpO2, IntradayResolution.Daily),
        };
        var range = DateRange.SingleDay(Jan1);
        var clock = new TestClock(Start);
        var provider = new RecordingHealthDataProvider(twoMetrics);
        var (engine, _, _, _) = Build(clock, provider, new RecordingMetricRepository());

        var result = await engine.RunBackfillAsync(BackfillCommand.For(range, [MetricType.SpO2]));

        result.Metrics.Should().ContainSingle();
        result.Metrics.Single().Metric.Should().Be(MetricType.SpO2);
        provider.FetchedDates.Should().ContainSingle();
    }
}
