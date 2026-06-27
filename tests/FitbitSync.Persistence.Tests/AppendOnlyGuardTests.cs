using FitbitSync.Persistence;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;

namespace FitbitSync.Persistence.Tests;

// Phase 2 security property: the audit ledger is append-only. The DbContext guard must reject any
// UPDATE or DELETE of an AuditEntryRow at SaveChanges time, while ordinary inserts (new appends)
// continue to succeed. Each operation uses a FRESH DbContext so the guard is exercised against the
// real encrypted database rather than a single tracker.
public sealed class AppendOnlyGuardTests : IDisposable
{
    private readonly EncryptedDatabaseFixture fixture = new();

    private static readonly DateTimeOffset EntryTimestamp = new(2024, 5, 1, 8, 0, 0, TimeSpan.Zero);

    private static AuditEntryRow NewEntry(long sequence, string action, string prevHash) =>
        new()
        {
            Sequence = sequence,
            Timestamp = EntryTimestamp,
            Action = action,
            PrevHash = prevHash,
            EntryHash = $"hash-{sequence}",
        };

    private async Task SeedEntryAsync()
    {
        using var context = this.fixture.NewDbContext();
        context.AuditEntries.Add(NewEntry(1, "login", "GENESIS"));
        await context.SaveChangesAsync();
    }

    [Fact]
    public async Task AuditEntry_Insert_Succeeds()
    {
        // Given a fresh context, appending a new audit entry is permitted...
        using var context = this.fixture.NewDbContext();
        context.AuditEntries.Add(NewEntry(1, "login", "GENESIS"));

        var append = async () => await context.SaveChangesAsync();
        await append.Should().NotThrowAsync();

        // ...and the row is persisted.
        using var verify = this.fixture.NewDbContext();
        verify.AuditEntries.Count().Should().Be(1);
    }

    [Fact]
    public async Task AuditEntry_Modify_Throws()
    {
        // Given an existing audit entry...
        await this.SeedEntryAsync();

        // When a fresh context loads and mutates it, SaveChanges must reject the UPDATE.
        using var context = this.fixture.NewDbContext();
        var row = await context.AuditEntries.SingleAsync(entry => entry.Sequence == 1);
        row.Action = "tampered";

        var modify = async () => await context.SaveChangesAsync();
        await modify.Should().ThrowAsync<InvalidOperationException>();
    }

    [Fact]
    public async Task AuditEntry_Delete_Throws()
    {
        // Given an existing audit entry...
        await this.SeedEntryAsync();

        // When a fresh context removes it, SaveChanges must reject the DELETE.
        using var context = this.fixture.NewDbContext();
        var row = await context.AuditEntries.SingleAsync(entry => entry.Sequence == 1);
        context.AuditEntries.Remove(row);

        var delete = async () => await context.SaveChangesAsync();
        await delete.Should().ThrowAsync<InvalidOperationException>();
    }

    public void Dispose() => this.fixture.Dispose();
}
