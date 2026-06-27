using FitbitSync.Domain;
using Microsoft.EntityFrameworkCore;

namespace FitbitSync.Persistence;

public sealed class AuditTrail : IAuditTrail
{
    private const string GenesisHash = "GENESIS";

    private readonly FitbitSyncDbContext dbContext;
    private readonly IClock clock;
    private readonly AuditEntryHasher hasher;

    public AuditTrail(FitbitSyncDbContext dbContext, IClock clock, AuditEntryHasher hasher)
    {
        ArgumentNullException.ThrowIfNull(dbContext);
        ArgumentNullException.ThrowIfNull(clock);
        ArgumentNullException.ThrowIfNull(hasher);

        this.dbContext = dbContext;
        this.clock = clock;
        this.hasher = hasher;
    }

    public async Task<AuditEntry> AppendAsync(string action, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(action);

        var head = await this.dbContext.AuditEntries
            .OrderByDescending(entry => entry.Sequence)
            .FirstOrDefaultAsync(ct)
            .ConfigureAwait(false);

        var sequence = (head?.Sequence ?? 0) + 1;
        var prevHash = head?.EntryHash ?? GenesisHash;
        var timestamp = this.clock.UtcNow;
        var entryHash = this.hasher.ComputeHash(sequence, timestamp, action, prevHash);

        this.dbContext.AuditEntries.Add(new AuditEntryRow
        {
            Sequence = sequence,
            Timestamp = timestamp,
            Action = action,
            PrevHash = prevHash,
            EntryHash = entryHash,
        });

        await this.dbContext.SaveChangesAsync(ct).ConfigureAwait(false);

        return new AuditEntry(sequence, timestamp, action, prevHash, entryHash);
    }

    public async Task<bool> VerifyChainAsync(CancellationToken ct = default)
    {
        var entries = await this.dbContext.AuditEntries
            .OrderBy(entry => entry.Sequence)
            .ToListAsync(ct)
            .ConfigureAwait(false);

        var expectedSequence = 1L;
        var expectedPrevHash = GenesisHash;

        foreach (var entry in entries)
        {
            if (entry.Sequence != expectedSequence)
            {
                return false;
            }

            if (entry.PrevHash != expectedPrevHash)
            {
                return false;
            }

            var recomputed = this.hasher.ComputeHash(entry.Sequence, entry.Timestamp, entry.Action, entry.PrevHash);
            if (recomputed != entry.EntryHash)
            {
                return false;
            }

            expectedSequence++;
            expectedPrevHash = entry.EntryHash;
        }

        return true;
    }
}
