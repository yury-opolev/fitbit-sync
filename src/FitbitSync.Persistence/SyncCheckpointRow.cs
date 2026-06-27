using FitbitSync.Domain;

namespace FitbitSync.Persistence;

public sealed class SyncCheckpointRow
{
    public MetricType Metric { get; set; }

    public DateTimeOffset? NewestSynced { get; set; }

    public DateTimeOffset? OldestBackfilled { get; set; }

    public Guid RowVersion { get; set; }
}
