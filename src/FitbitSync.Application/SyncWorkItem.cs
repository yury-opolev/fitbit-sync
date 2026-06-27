using FitbitSync.Domain;

namespace FitbitSync.Application;

public sealed record SyncWorkItem(
    MetricType Metric,
    IntradayResolution Resolution,
    DateOnly Date,
    SyncWorkKind Kind);
