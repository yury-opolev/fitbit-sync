using FitbitSync.Domain;

namespace FitbitSync.Application.Tests;

// Phase 5 NAMED red test: SyncEngine_ResumesFromCheckpoint_AfterSimulatedCrash.
// The engine must persist a durable checkpoint after every completed day so a crash (modelled as the
// CancellationToken tripping mid-run) loses no progress: a second run re-plans from the persisted
// checkpoints, fetches ONLY the days not yet done (no duplicates), and the append-only audit chain stays
// valid across both runs. This proves resumable/incremental sync survives restarts (§5.2, §8.2).
public sealed class SyncEngineResumabilityTests
{
    private static readonly DateTimeOffset Start = new(2024, 5, 10, 9, 0, 0, TimeSpan.Zero);

    private static readonly IReadOnlyList<MetricCapability> OneDailyMetric =
        [new MetricCapability(MetricType.SpO2, IntradayResolution.Daily)];

    private static SyncEngine BuildEngine(
        IHealthDataProvider provider,
        ISyncCheckpointStore checkpoints,
        IMetricRepository repository,
        IAuditTrail audit,
        IClock clock)
    {
        var options = new SyncOptions { BackfillWindow = TimeSpan.FromDays(2), RetryBaseDelay = TimeSpan.Zero };
        var gate = new TokenBucketRateLimitGate(clock, options);
        var planner = new SyncPlanner(options);
        var resilience = new SyncResiliencePipelineProvider(options);
        return new SyncEngine(provider, repository, checkpoints, audit, gate, planner, resilience, clock);
    }

    [Fact]
    public async Task SyncEngine_ResumesFromCheckpoint_AfterSimulatedCrash()
    {
        var clock = new TestClock(Start);
        var checkpoints = new InMemoryCheckpointStore();
        var repository = new RecordingMetricRepository();
        var audit = new InMemoryAuditTrail();

        // Plan for SpO2 on 2024-05-10: incremental today + backfill 05-09, 05-08 => 3 days total.
        using var crashAfterTwo = new CancellationTokenSource();
        var crashingProvider = new RecordingHealthDataProvider(
            OneDailyMetric,
            afterFetch: self =>
            {
                // Simulate a crash: after the 2nd successful day, cancel before the 3rd is processed.
                if (self.FetchedDates.Count == 2)
                {
                    crashAfterTwo.Cancel();
                }
            });

        var firstEngine = BuildEngine(crashingProvider, checkpoints, repository, audit, clock);

        var firstRun = await firstEngine.RunOnceAsync(crashAfterTwo.Token);

        // The run stopped early (cancelled) having completed exactly two days.
        firstRun.Outcome.Should().Be(SyncRunOutcome.Cancelled);
        firstRun.ItemsCompleted.Should().Be(2);
        crashingProvider.FetchedDates.Should().HaveCount(2);
        var datesBeforeCrash = crashingProvider.FetchedDates.ToList();

        // --- Restart: a fresh provider + engine, same durable stores. ---
        var resumeProvider = new RecordingHealthDataProvider(OneDailyMetric);
        var secondEngine = BuildEngine(resumeProvider, checkpoints, repository, audit, clock);

        var secondRun = await secondEngine.RunOnceAsync();

        secondRun.Outcome.Should().Be(SyncRunOutcome.Completed);

        // The second run fetched only the day(s) not done before the crash — no duplicates.
        resumeProvider.FetchedDates.Should().NotBeEmpty();
        resumeProvider.FetchedDates.Should().NotIntersectWith(datesBeforeCrash);

        // Across both runs every planned day was fetched exactly once.
        var allFetched = datesBeforeCrash.Concat(resumeProvider.FetchedDates).ToList();
        allFetched.Should().OnlyHaveUniqueItems();
        allFetched.Should().BeEquivalentTo(
        [
            new DateOnly(2024, 5, 10),
            new DateOnly(2024, 5, 9),
            new DateOnly(2024, 5, 8),
        ]);

        // The append-only audit chain remains valid across the crash + resume.
        (await audit.VerifyChainAsync()).Should().BeTrue();
    }
}
