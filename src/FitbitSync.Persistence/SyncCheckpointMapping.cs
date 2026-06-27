using FitbitSync.Domain;

namespace FitbitSync.Persistence;

internal static class SyncCheckpointMapping
{
    public static SyncCheckpointRow ToRow(SyncCheckpoint checkpoint) =>
        new()
        {
            Metric = checkpoint.Metric,
            NewestSynced = checkpoint.NewestSynced,
            OldestBackfilled = checkpoint.OldestBackfilled,
            RowVersion = Guid.NewGuid(),
        };

    public static SyncCheckpoint ToDomain(SyncCheckpointRow row) =>
        new(row.Metric, row.NewestSynced, row.OldestBackfilled);
}
