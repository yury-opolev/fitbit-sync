namespace FitbitSync.Domain;

public interface IMetricRepository
{
    Task UpsertAsync(IReadOnlyCollection<MetricSample> samples, CancellationToken ct = default);

    Task<SyncCheckpoint?> GetCheckpointAsync(MetricType metric, CancellationToken ct = default);

    Task<IReadOnlyList<DateOnly>> GetCoveredDatesAsync(MetricType metric, DateRange range, CancellationToken ct = default);

    Task<IReadOnlyList<MetricSample>> QueryAsync(MetricType metric, DateRange range, CancellationToken ct = default);
}
