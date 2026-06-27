using FitbitSync.Domain;
using Microsoft.EntityFrameworkCore;

namespace FitbitSync.Persistence;

public sealed class SyncCheckpointStore : ISyncCheckpointStore
{
    private readonly FitbitSyncDbContext dbContext;

    public SyncCheckpointStore(FitbitSyncDbContext dbContext)
    {
        ArgumentNullException.ThrowIfNull(dbContext);
        this.dbContext = dbContext;
    }

    public async Task<SyncCheckpoint?> GetAsync(MetricType metric, CancellationToken ct = default)
    {
        var row = await this.dbContext.SyncCheckpoints
            .SingleOrDefaultAsync(checkpoint => checkpoint.Metric == metric, ct)
            .ConfigureAwait(false);

        return row is null ? null : SyncCheckpointMapping.ToDomain(row);
    }

    public async Task SaveAsync(SyncCheckpoint checkpoint, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(checkpoint);

        var existing = await this.dbContext.SyncCheckpoints
            .SingleOrDefaultAsync(row => row.Metric == checkpoint.Metric, ct)
            .ConfigureAwait(false);

        if (existing is null)
        {
            this.dbContext.SyncCheckpoints.Add(SyncCheckpointMapping.ToRow(checkpoint));
        }
        else
        {
            existing.NewestSynced = checkpoint.NewestSynced;
            existing.OldestBackfilled = checkpoint.OldestBackfilled;
            existing.RowVersion = Guid.NewGuid();
        }

        await this.dbContext.SaveChangesAsync(ct).ConfigureAwait(false);
    }
}
