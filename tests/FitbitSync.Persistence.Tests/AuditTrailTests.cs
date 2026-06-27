using FitbitSync.Domain;
using FitbitSync.Persistence;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;

namespace FitbitSync.Persistence.Tests;

// Phase 2 security properties for the append-only audit trail: appends form a hash chain with
// incrementing sequence and prev_hash == previous entry_hash (the first linking to GENESIS),
// VerifyChainAsync confirms an intact chain, and any out-of-band tamper (raw SQL that bypasses the
// append-only guard) breaks verification. Each operation uses a FRESH DbContext.
public sealed class AuditTrailTests : IDisposable
{
    private const string GenesisHash = "GENESIS";

    private readonly EncryptedDatabaseFixture fixture = new();
    private readonly AuditEntryHasher hasher = new();
    private readonly MutableClock clock = new();

    private AuditTrail NewAuditTrail(FitbitSyncDbContext context) =>
        new(context, this.clock, this.hasher);

    private async Task<AuditEntry> AppendThroughFreshContextAsync(string action)
    {
        using var context = this.fixture.NewDbContext();
        var trail = this.NewAuditTrail(context);
        return await trail.AppendAsync(action);
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
    public async Task AuditTrail_Append_BuildsIncrementingHashChain()
    {
        // Given a sequence of appends, each advancing the fake clock...
        this.clock.UtcNow = new DateTimeOffset(2024, 5, 1, 8, 0, 0, TimeSpan.Zero);
        var first = await this.AppendThroughFreshContextAsync("login");

        this.clock.UtcNow = this.clock.UtcNow.AddMinutes(1);
        var second = await this.AppendThroughFreshContextAsync("sync");

        this.clock.UtcNow = this.clock.UtcNow.AddMinutes(1);
        var third = await this.AppendThroughFreshContextAsync("logout");

        // Then sequence increments and each prev_hash links to the prior entry_hash (first == genesis).
        first.Sequence.Should().Be(1);
        second.Sequence.Should().Be(2);
        third.Sequence.Should().Be(3);

        first.PrevHash.Should().Be(GenesisHash);
        second.PrevHash.Should().Be(first.EntryHash);
        third.PrevHash.Should().Be(second.EntryHash);
    }

    [Fact]
    public async Task AuditTrail_VerifyChain_ReturnsTrue_ForIntactChain()
    {
        // Given several appends...
        await this.AppendThroughFreshContextAsync("login");
        await this.AppendThroughFreshContextAsync("sync");
        await this.AppendThroughFreshContextAsync("logout");

        // Then the chain verifies.
        using var context = this.fixture.NewDbContext();
        var trail = this.NewAuditTrail(context);
        (await trail.VerifyChainAsync()).Should().BeTrue();
    }

    [Fact]
    public async Task AuditTrail_VerifyChain_ReturnsFalse_WhenActionTampered()
    {
        // Given an intact chain...
        await this.AppendThroughFreshContextAsync("login");
        await this.AppendThroughFreshContextAsync("sync");

        // When a stored row's action is mutated out-of-band (raw SQL bypasses the append-only guard)...
        this.ExecuteRawSql("UPDATE audit_entries SET action = 'tampered' WHERE sequence = 1;");

        // Then the recomputed hash no longer matches and verification fails.
        using var context = this.fixture.NewDbContext();
        var trail = this.NewAuditTrail(context);
        (await trail.VerifyChainAsync()).Should().BeFalse();
    }

    [Fact]
    public async Task AuditTrail_VerifyChain_ReturnsFalse_WhenEntryHashTampered()
    {
        // Given an intact chain...
        await this.AppendThroughFreshContextAsync("login");
        await this.AppendThroughFreshContextAsync("sync");

        // When a stored row's entry_hash is overwritten out-of-band...
        this.ExecuteRawSql("UPDATE audit_entries SET entry_hash = 'deadbeef' WHERE sequence = 1;");

        // Then both the self-hash check and the next row's prev_hash linkage break verification.
        using var context = this.fixture.NewDbContext();
        var trail = this.NewAuditTrail(context);
        (await trail.VerifyChainAsync()).Should().BeFalse();
    }

    public void Dispose() => this.fixture.Dispose();

    private sealed class MutableClock : IClock
    {
        public DateTimeOffset UtcNow { get; set; } = DateTimeOffset.UnixEpoch;
    }
}
