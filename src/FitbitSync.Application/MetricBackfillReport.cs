using FitbitSync.Domain;

namespace FitbitSync.Application;

public sealed record MetricBackfillReport(
    MetricType Metric,
    IReadOnlyList<DateOnly> AlreadyCovered,
    IReadOnlyList<DateOnly> Fetched,
    IReadOnlyList<DateOnly> StillMissing,
    int SamplesWritten);
