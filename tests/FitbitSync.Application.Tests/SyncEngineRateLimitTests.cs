using FitbitSync.Domain;

namespace FitbitSync.Application.Tests;

// Phase 5 (5h): rate-limit handling end-to-end through the engine. A provider 429 (surfaced as the
// provider-neutral ProviderRateLimitedException) must: stop the current run with outcome RateLimited,
// pause the gate until the snapshot's reset instant, and write a "RateLimited" audit entry — leaving
// checkpoints intact so the NEXT run (after the reset window) resumes from where it stopped. Likewise an
// already-exhausted budget stops the run without even calling the provider. All time is clock-driven.
public sealed class SyncEngineRateLimitTests
{
    private static readonly DateTimeOffset Start = new(2024, 5, 10, 9, 0, 0, TimeSpan.Zero);

    private static readonly IReadOnlyList<MetricCapability> OneDailyMetric =
        [new MetricCapability(MetricType.SpO2, IntradayResolution.Daily)];

    private static (SyncEngine Engine, TokenBucketRateLimitGate Gate, InMemoryAuditTrail Audit, RecordingHealthDataProvider Provider, InMemoryCheckpointStore Checkpoints)
        Build(TestClock clock, RecordingHealthDataProvider provider, int budget = 150)
    {
        var options = new SyncOptions
        {
            BackfillWindow = TimeSpan.FromDays(2),
            RetryBaseDelay = TimeSpan.Zero,
            HourlyRequestBudget = budget,
        };
        var gate = new TokenBucketRateLimitGate(clock, options);
        var checkpoints = new InMemoryCheckpointStore();
        var audit = new InMemoryAuditTrail();
        var engine = new SyncEngine(
            provider,
            new RecordingMetricRepository(),
            checkpoints,
            audit,
            gate,
            new SyncPlanner(options),
            new SyncResiliencePipelineProvider(options),
            clock);
        return (engine, gate, audit, provider, checkpoints);
    }

    [Fact]
    public async Task RunOnce_On429_PausesGate_AndAuditsRateLimited()
    {
        var clock = new TestClock(Start);
        var throwOn = new RecordingHealthDataProvider(OneDailyMetric)
        {
            FetchOverride = request =>
                throw new ProviderRateLimitedException(new RateLimitSnapshot(0, 150, 900, clock.UtcNow)),
        };
        var (engine, gate, audit, _, _) = Build(clock, throwOn);

        var run = await engine.RunOnceAsync();

        run.Outcome.Should().Be(SyncRunOutcome.RateLimited);
        gate.IsPaused.Should().BeTrue();
        gate.PausedUntil.Should().Be(Start.AddSeconds(900));
        audit.Actions.Should().Contain("RateLimited");
    }

    [Fact]
    public async Task RunOnce_WhenBudgetExhausted_StopsWithoutCallingProvider()
    {
        var clock = new TestClock(Start);
        var provider = new RecordingHealthDataProvider(OneDailyMetric);
        var (engine, gate, _, _, _) = Build(clock, provider, budget: 0);

        var run = await engine.RunOnceAsync();

        run.Outcome.Should().Be(SyncRunOutcome.RateLimited);
        run.ItemsCompleted.Should().Be(0);
        provider.FetchedDates.Should().BeEmpty();
    }

    [Fact]
    public async Task RunOnce_AfterPauseElapses_ResumesAndCompletes()
    {
        var clock = new TestClock(Start);

        // First run: every fetch 429s, so nothing completes and the gate pauses for 900s.
        var blocked = new RecordingHealthDataProvider(OneDailyMetric)
        {
            FetchOverride = request =>
                throw new ProviderRateLimitedException(new RateLimitSnapshot(0, 150, 900, clock.UtcNow)),
        };
        var (engine, gate, _, _, checkpoints) = Build(clock, blocked);

        var first = await engine.RunOnceAsync();
        first.Outcome.Should().Be(SyncRunOutcome.RateLimited);
        first.ItemsCompleted.Should().Be(0);
        checkpoints.SaveCount.Should().Be(0);

        // Time passes beyond the reset window; the gate auto-resumes.
        clock.Advance(TimeSpan.FromSeconds(901));
        gate.IsPaused.Should().BeFalse();

        // Second run with a healthy provider completes the full plan.
        var healthy = new RecordingHealthDataProvider(OneDailyMetric);
        var resumeOptions = new SyncOptions { BackfillWindow = TimeSpan.FromDays(2), RetryBaseDelay = TimeSpan.Zero };
        var resumeEngine = new SyncEngine(
            healthy,
            new RecordingMetricRepository(),
            checkpoints,
            new InMemoryAuditTrail(),
            gate,
            new SyncPlanner(resumeOptions),
            new SyncResiliencePipelineProvider(resumeOptions),
            clock);

        var second = await resumeEngine.RunOnceAsync();

        second.Outcome.Should().Be(SyncRunOutcome.Completed);
        // today + 2 backfill days.
        healthy.FetchedDates.Should().HaveCount(3);
    }
}
