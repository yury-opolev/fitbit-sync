using FitbitSync.Domain;
using FitbitSync.Persistence;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;

namespace FitbitSync.Persistence.Tests;

// Phase 7 first red tests. IntegrityVerifier combines two tamper-evidence controls over the real
// encrypted database: the audit hash-chain (IAuditTrail.VerifyChainAsync) and per-row signature
// re-verification of every metric_samples row (IRecordSigner.Verify). Out-of-band tampering via raw
// SQL (which bypasses the append-only guard and the signing path) must be detected. Each operation
// uses a FRESH DbContext so detection happens at the database, not in one change-tracker.
public sealed class IntegrityVerifierTests : IDisposable
{
    private readonly EncryptedDatabaseFixture fixture = new();
    private readonly AuditEntryHasher hasher = new();
    private readonly MutableClock clock = new();

    private static readonly DateTimeOffset SampleTimestamp = new(2024, 5, 1, 10, 0, 0, TimeSpan.Zero);

    private IntegrityVerifier NewVerifier(FitbitSyncDbContext context)
    {
        var auditTrail = new AuditTrail(context, this.clock, this.hasher);
        return new IntegrityVerifier(context, auditTrail, this.fixture.RecordSigner);
    }

    private async Task AppendAuditAsync(string action)
    {
        using var context = this.fixture.NewDbContext();
        await new AuditTrail(context, this.clock, this.hasher).AppendAsync(action);
    }

    private async Task UpsertSampleAsync(MetricSample sample)
    {
        using var context = this.fixture.NewDbContext();
        var repository = new MetricRepository(context, this.fixture.RecordSigner, this.fixture.KeyProvider);
        await repository.UpsertAsync([sample]);
    }

    private void ExecuteRawSql(string sql)
    {
        using var context = this.fixture.NewDbContext();
        var connection = context.Database.GetDbConnection();
        using var command = connection.CreateCommand();
        command.CommandText = sql;
        command.ExecuteNonQuery();
    }

    [Fact]
    public async Task VerifyIntegrity_ReturnsValid_ForUntamperedDatabase()
    {
        // Given an intact audit chain and signed samples...
        await this.AppendAuditAsync("login");
        await this.UpsertSampleAsync(new MetricSample(MetricType.HeartRate, SampleTimestamp, 72, "bpm", IntradayResolution.OneMinute, "fitbit"));
        await this.UpsertSampleAsync(new MetricSample(MetricType.SpO2, SampleTimestamp, 97, "percent", IntradayResolution.Daily, "fitbit"));

        // Then the report is fully valid: chain intact, both samples verified, none forged.
        using var context = this.fixture.NewDbContext();
        var report = await this.NewVerifier(context).VerifyAsync();

        report.IsAuditChainIntact.Should().BeTrue();
        report.VerifiedSampleCount.Should().Be(2);
        report.ForgedSampleCount.Should().Be(0);
        report.IsValid.Should().BeTrue();
    }

    [Fact]
    public async Task VerifyChain_DetectsTamperedAuditEntry()
    {
        // Given an intact audit chain...
        await this.AppendAuditAsync("login");
        await this.AppendAuditAsync("sync");

        // When an audit row's action is mutated out-of-band (raw SQL bypasses the append-only guard)...
        this.ExecuteRawSql("UPDATE audit_entries SET action = 'tampered' WHERE sequence = 1;");

        // Then the chain is reported broken and the overall report is invalid.
        using var context = this.fixture.NewDbContext();
        var report = await this.NewVerifier(context).VerifyAsync();

        report.IsAuditChainIntact.Should().BeFalse();
        report.IsValid.Should().BeFalse();
    }

    [Fact]
    public async Task VerifyIntegrity_DetectsForgedSample()
    {
        // Given two signed samples...
        await this.UpsertSampleAsync(new MetricSample(MetricType.HeartRate, SampleTimestamp, 72, "bpm", IntradayResolution.OneMinute, "fitbit"));
        await this.UpsertSampleAsync(new MetricSample(MetricType.HeartRate, SampleTimestamp.AddMinutes(1), 75, "bpm", IntradayResolution.OneMinute, "fitbit"));

        // When one row's value is forged out-of-band, leaving its old signature in place...
        this.ExecuteRawSql("UPDATE metric_samples SET value = 999 WHERE value = 72;");

        // Then the signature no longer matches the canonical bytes and exactly one forgery is detected.
        using var context = this.fixture.NewDbContext();
        var report = await this.NewVerifier(context).VerifyAsync();

        report.ForgedSampleCount.Should().Be(1);
        report.VerifiedSampleCount.Should().Be(1);
        report.IsValid.Should().BeFalse();
    }

    public void Dispose() => this.fixture.Dispose();

    private sealed class MutableClock : IClock
    {
        public DateTimeOffset UtcNow { get; set; } = DateTimeOffset.UnixEpoch;
    }
}
