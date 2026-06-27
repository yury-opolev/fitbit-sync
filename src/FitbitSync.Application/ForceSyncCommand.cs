using FitbitSync.Domain;

namespace FitbitSync.Application;

public sealed record ForceSyncCommand(Guid RunId, IReadOnlyList<MetricType>? Metrics, DateRange? Range)
{
    public static ForceSyncCommand ForAll() => new(Guid.NewGuid(), null, null);

    public static ForceSyncCommand For(IReadOnlyList<MetricType> metrics, DateRange? range = null) =>
        new(Guid.NewGuid(), metrics, range);
}
