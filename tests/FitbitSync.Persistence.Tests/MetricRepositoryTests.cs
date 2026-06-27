using FitbitSync.Domain;
using FitbitSync.Persistence;
using FluentAssertions;

namespace FitbitSync.Persistence.Tests;

// Phase 2 first red test: the metric repository must upsert idempotently against the unique
// (Source, Type, Resolution, Timestamp) index and sign every row. Re-fetching the same logical
// sample overwrites the value and re-signs rather than inserting a duplicate. Each operation uses
// a FRESH DbContext so the dedup is enforced at the encrypted database, not just in one tracker.
public sealed class MetricRepositoryTests : IDisposable
{
    private readonly EncryptedDatabaseFixture fixture = new();

    private static readonly DateTimeOffset SampleTimestamp = new(2024, 5, 1, 10, 0, 0, TimeSpan.Zero);

    private async Task UpsertThroughFreshContextAsync(params MetricSample[] samples)
    {
        using var context = this.fixture.NewDbContext();
        var repository = new MetricRepository(context, this.fixture.RecordSigner, this.fixture.KeyProvider);
        await repository.UpsertAsync(samples);
    }

    [Fact]
    public async Task MetricRepository_UpsertIsIdempotent_AndSignsRows()
    {
        // Given the same logical sample (same key) upserted twice with a changed value...
        var first = new MetricSample(MetricType.HeartRate, SampleTimestamp, 72, "bpm", IntradayResolution.OneMinute, "fitbit");
        var second = first with { Value = 80 };

        await this.UpsertThroughFreshContextAsync(first);
        await this.UpsertThroughFreshContextAsync(second);

        // Then exactly ONE row exists for that key (no duplicate), with the overwritten value...
        using var verify = this.fixture.NewDbContext();
        var rows = verify.MetricSamples
            .Where(row => row.Source == "fitbit"
                && row.Type == MetricType.HeartRate
                && row.Resolution == IntradayResolution.OneMinute)
            .ToList();

        rows.Should().HaveCount(1);
        rows[0].Value.Should().Be(80);

        // And the row is signed: signature bytes and the signing-key id are populated.
        rows[0].Signature.Should().NotBeEmpty();
        rows[0].SignatureKeyId.Should().Be(this.fixture.KeyProvider.SigningKeyId);
    }

    [Fact]
    public async Task MetricRepository_StoresDistinctRows_ForDifferentKeys()
    {
        // Given two samples that differ only by timestamp (a distinct key)...
        var first = new MetricSample(MetricType.HeartRate, SampleTimestamp, 72, "bpm", IntradayResolution.OneMinute, "fitbit");
        var second = first with { Timestamp = SampleTimestamp.AddMinutes(1), Value = 75 };

        await this.UpsertThroughFreshContextAsync(first, second);

        // Then both are persisted as separate rows.
        using var verify = this.fixture.NewDbContext();
        verify.MetricSamples.Count(row => row.Type == MetricType.HeartRate).Should().Be(2);
    }

    public void Dispose() => this.fixture.Dispose();
}
