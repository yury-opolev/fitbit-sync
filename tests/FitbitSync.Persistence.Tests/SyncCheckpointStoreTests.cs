using FitbitSync.Domain;
using FitbitSync.Persistence;
using FluentAssertions;

namespace FitbitSync.Persistence.Tests;

// The checkpoint store round-trips the per-metric cursor through the encrypted database: the
// forward incremental position (NewestSynced) and the backwards backfill position
// (OldestBackfilled) must both persist and reload exactly. Each operation uses a fresh DbContext.
public sealed class SyncCheckpointStoreTests : IDisposable
{
    private readonly EncryptedDatabaseFixture fixture = new();

    private static readonly DateTimeOffset Newest = new(2024, 5, 10, 0, 0, 0, TimeSpan.Zero);
    private static readonly DateTimeOffset Oldest = new(2024, 4, 1, 0, 0, 0, TimeSpan.Zero);

    private async Task SaveThroughFreshContextAsync(SyncCheckpoint checkpoint)
    {
        using var context = this.fixture.NewDbContext();
        await new SyncCheckpointStore(context).SaveAsync(checkpoint);
    }

    private async Task<SyncCheckpoint?> GetThroughFreshContextAsync(MetricType metric)
    {
        using var context = this.fixture.NewDbContext();
        return await new SyncCheckpointStore(context).GetAsync(metric);
    }

    [Fact]
    public async Task Save_ThenGet_RoundTripsBothPositions()
    {
        // Given a checkpoint with both a forward and a backfill position...
        var checkpoint = new SyncCheckpoint(MetricType.HeartRate, Newest, Oldest);
        await this.SaveThroughFreshContextAsync(checkpoint);

        // When reloaded through a fresh store, both positions are preserved exactly.
        var loaded = await this.GetThroughFreshContextAsync(MetricType.HeartRate);

        loaded.Should().NotBeNull();
        loaded!.Metric.Should().Be(MetricType.HeartRate);
        loaded.NewestSynced.Should().Be(Newest);
        loaded.OldestBackfilled.Should().Be(Oldest);
    }

    [Fact]
    public async Task Save_Twice_UpdatesTheSingleRow_PerMetric()
    {
        // Given an initial checkpoint that is later advanced...
        await this.SaveThroughFreshContextAsync(new SyncCheckpoint(MetricType.HeartRate, Newest, Oldest));
        var advanced = new SyncCheckpoint(MetricType.HeartRate, Newest.AddDays(1), Oldest.AddDays(-1));
        await this.SaveThroughFreshContextAsync(advanced);

        // Then there is still exactly one row for that metric, holding the advanced positions.
        using (var verify = this.fixture.NewDbContext())
        {
            verify.SyncCheckpoints.Count(row => row.Metric == MetricType.HeartRate).Should().Be(1);
        }

        var loaded = await this.GetThroughFreshContextAsync(MetricType.HeartRate);
        loaded!.NewestSynced.Should().Be(Newest.AddDays(1));
        loaded.OldestBackfilled.Should().Be(Oldest.AddDays(-1));
    }

    [Fact]
    public async Task Get_ReturnsNull_WhenNoCheckpointExists()
    {
        // Given no checkpoint has been saved for a metric, Get returns null.
        var loaded = await this.GetThroughFreshContextAsync(MetricType.Sleep);

        loaded.Should().BeNull();
    }

    public void Dispose() => this.fixture.Dispose();
}
