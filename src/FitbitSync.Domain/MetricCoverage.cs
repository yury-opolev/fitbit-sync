namespace FitbitSync.Domain;

public sealed record MetricCoverage(
    MetricType Metric,
    DateRange RequestedRange,
    DateOnly? HeldFrom,
    DateOnly? HeldTo,
    int DaysHeld,
    IReadOnlyList<DateOnly> Gaps);
