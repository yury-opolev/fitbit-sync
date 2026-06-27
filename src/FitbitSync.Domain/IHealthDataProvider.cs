namespace FitbitSync.Domain;

public interface IHealthDataProvider
{
    string Source { get; }

    IReadOnlyList<MetricCapability> Capabilities { get; }

    Task<MetricFetchResult> FetchAsync(MetricFetchRequest request, CancellationToken ct = default);
}
