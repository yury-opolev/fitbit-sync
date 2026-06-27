using FitbitSync.Domain;
using FitbitSync.Persistence;
using FluentAssertions;

namespace FitbitSync.Persistence.Tests;

// Phase 8: the read path the agent verbs (query + gap-aware backfill) consume. GetCoveredDatesAsync returns
// the DISTINCT UTC dates actually present in metric_samples for a metric within a range — the truthful
// "what we hold", so an INTERIOR gap is detected, not masked by a checkpoint high-water mark. QueryAsync
// returns the samples ordered by timestamp. Exercised against the REAL encrypted SQLite database.
public sealed class MetricRepositoryReadPathTests : IDisposable
{
    private readonly EncryptedDatabaseFixture fixture = new();

    private static readonly DateOnly Jan1 = new(2024, 1, 1);

    private async Task SeedAsync(params MetricSample[] samples)
    {
        using var context = this.fixture.NewDbContext();
        var repository = new MetricRepository(context, this.fixture.RecordSigner, this.fixture.KeyProvider);
        await repository.UpsertAsync(samples);
    }

    private static MetricSample HeartRateOn(DateOnly date, int hour, double value)
    {
        var timestamp = new DateTimeOffset(date.ToDateTime(new TimeOnly(hour, 0)), TimeSpan.Zero);
        return new MetricSample(MetricType.HeartRate, timestamp, value, "bpm", IntradayResolution.OneMinute, "fitbit");
    }

    [Fact]
    public async Task GetCoveredDates_ReturnsDistinctOrderedDates_DetectingInteriorGap()
    {
        // Hold Jan 1 (twice, distinct hours), skip Jan 2, hold Jan 3 — the gap on Jan 2 must show as absent.
        await this.SeedAsync(
            HeartRateOn(Jan1, 8, 60),
            HeartRateOn(Jan1, 9, 61),
            HeartRateOn(Jan1.AddDays(2), 8, 62));

        using var context = this.fixture.NewDbContext();
        var repository = new MetricRepository(context, this.fixture.RecordSigner, this.fixture.KeyProvider);

        var covered = await repository.GetCoveredDatesAsync(MetricType.HeartRate, new DateRange(Jan1, Jan1.AddDays(4)));

        covered.Should().Equal(Jan1, Jan1.AddDays(2));
    }

    [Fact]
    public async Task GetCoveredDates_EmptyStore_ReturnsEmpty()
    {
        using var context = this.fixture.NewDbContext();
        var repository = new MetricRepository(context, this.fixture.RecordSigner, this.fixture.KeyProvider);

        var covered = await repository.GetCoveredDatesAsync(MetricType.SpO2, new DateRange(Jan1, Jan1.AddDays(4)));

        covered.Should().BeEmpty();
    }

    [Fact]
    public async Task GetCoveredDates_IsScopedToMetricAndRange()
    {
        // A different metric on Jan 1, and a HeartRate sample OUTSIDE the queried range, are both excluded.
        await this.SeedAsync(
            HeartRateOn(Jan1, 8, 60),
            HeartRateOn(Jan1.AddDays(10), 8, 70),
            new MetricSample(MetricType.SpO2, new DateTimeOffset(Jan1.ToDateTime(TimeOnly.MinValue), TimeSpan.Zero), 97, "percent", IntradayResolution.Daily, "fitbit"));

        using var context = this.fixture.NewDbContext();
        var repository = new MetricRepository(context, this.fixture.RecordSigner, this.fixture.KeyProvider);

        var covered = await repository.GetCoveredDatesAsync(MetricType.HeartRate, new DateRange(Jan1, Jan1.AddDays(4)));

        covered.Should().Equal(Jan1);
    }

    [Fact]
    public async Task QueryAsync_ReturnsSamples_OrderedByTimestamp_WithinRange()
    {
        await this.SeedAsync(
            HeartRateOn(Jan1.AddDays(2), 8, 62),
            HeartRateOn(Jan1, 9, 61),
            HeartRateOn(Jan1, 8, 60),
            HeartRateOn(Jan1.AddDays(10), 8, 99));

        using var context = this.fixture.NewDbContext();
        var repository = new MetricRepository(context, this.fixture.RecordSigner, this.fixture.KeyProvider);

        var samples = await repository.QueryAsync(MetricType.HeartRate, new DateRange(Jan1, Jan1.AddDays(4)));

        samples.Select(sample => sample.Value).Should().Equal(60, 61, 62);
        samples.Should().OnlyContain(sample => sample.Type == MetricType.HeartRate);
    }

    [Fact]
    public async Task QueryAsync_EmptyResult_ReturnsEmpty_NotNull()
    {
        using var context = this.fixture.NewDbContext();
        var repository = new MetricRepository(context, this.fixture.RecordSigner, this.fixture.KeyProvider);

        var samples = await repository.QueryAsync(MetricType.Sleep, new DateRange(Jan1, Jan1.AddDays(4)));

        samples.Should().NotBeNull().And.BeEmpty();
    }

    public void Dispose() => this.fixture.Dispose();
}
