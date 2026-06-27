namespace FitbitSync.Application;

public sealed record BackfillResult(
    Guid RunId,
    IReadOnlyList<MetricBackfillReport> Metrics,
    SyncRunOutcome Outcome);
