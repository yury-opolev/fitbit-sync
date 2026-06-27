namespace FitbitSync.Domain;

public interface ISyncCheckpointStore
{
    Task<SyncCheckpoint?> GetAsync(MetricType metric, CancellationToken ct = default);

    Task SaveAsync(SyncCheckpoint checkpoint, CancellationToken ct = default);
}
