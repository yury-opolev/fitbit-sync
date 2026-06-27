using FitbitSync.Domain;

namespace FitbitSync.Application;

public sealed record BackfillCommand(Guid RunId, IReadOnlyList<MetricType>? Metrics, DateRange Range)
{
    public static BackfillCommand For(DateRange range, IReadOnlyList<MetricType>? metrics = null) =>
        new(Guid.NewGuid(), metrics, range);
}
