namespace FitbitSync.Domain;

public sealed record MetricSample(
    MetricType Type,
    DateTimeOffset Timestamp,
    double Value,
    string Unit,
    IntradayResolution Resolution,
    string Source);
