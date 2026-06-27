namespace FitbitSync.Domain.Tests;

// SyncCheckpoint is the durable per-metric cursor: NewestSynced walks forward for
// incremental "catch up to today" sync, while OldestBackfilled walks backward for
// day-by-day historical backfill. The advance helpers must be monotonic in their
// respective directions so a late/out-of-order call can never regress progress.
public sealed class SyncCheckpointTests
{
    private static readonly DateTimeOffset Earlier = new(2024, 5, 1, 0, 0, 0, TimeSpan.Zero);
    private static readonly DateTimeOffset Later = new(2024, 5, 10, 0, 0, 0, TimeSpan.Zero);

    [Fact]
    public void AdvanceForward_MovesNewestSynced_Forward()
    {
        // Given a checkpoint already synced through the earlier date...
        var checkpoint = new SyncCheckpoint(MetricType.HeartRate, Earlier, null);

        // When advancing to a later date, NewestSynced moves forward to it.
        var advanced = checkpoint.AdvanceForward(Later);

        advanced.NewestSynced.Should().Be(Later);
    }

    [Fact]
    public void AdvanceForward_NeverMovesNewestSynced_Backward()
    {
        // Given a checkpoint already synced through the later date...
        var checkpoint = new SyncCheckpoint(MetricType.HeartRate, Later, null);

        // When an out-of-order advance to an earlier date arrives, progress is retained (later wins).
        var advanced = checkpoint.AdvanceForward(Earlier);

        advanced.NewestSynced.Should().Be(Later);
    }

    [Fact]
    public void AdvanceForward_FromNull_SetsNewestSynced()
    {
        // Given a brand-new checkpoint with no forward progress yet...
        var checkpoint = new SyncCheckpoint(MetricType.HeartRate, null, null);

        // When advancing for the first time, NewestSynced is initialized to the target.
        var advanced = checkpoint.AdvanceForward(Later);

        advanced.NewestSynced.Should().Be(Later);
    }

    [Fact]
    public void ExtendBackfill_MovesOldestBackfilled_Earlier()
    {
        // Given a checkpoint backfilled down to the later date...
        var checkpoint = new SyncCheckpoint(MetricType.HeartRate, null, Later);

        // When extending backfill to an earlier date, OldestBackfilled moves back to it.
        var extended = checkpoint.ExtendBackfill(Earlier);

        extended.OldestBackfilled.Should().Be(Earlier);
    }

    [Fact]
    public void ExtendBackfill_NeverMovesOldestBackfilled_Forward()
    {
        // Given a checkpoint already backfilled down to the earlier date...
        var checkpoint = new SyncCheckpoint(MetricType.HeartRate, null, Earlier);

        // When an out-of-order extend to a later date arrives, the deeper floor is retained (earlier wins).
        var extended = checkpoint.ExtendBackfill(Later);

        extended.OldestBackfilled.Should().Be(Earlier);
    }

    [Fact]
    public void ExtendBackfill_FromNull_SetsOldestBackfilled()
    {
        // Given a brand-new checkpoint with no backfill progress yet...
        var checkpoint = new SyncCheckpoint(MetricType.HeartRate, null, null);

        // When extending for the first time, OldestBackfilled is initialized to the target.
        var extended = checkpoint.ExtendBackfill(Earlier);

        extended.OldestBackfilled.Should().Be(Earlier);
    }
}
