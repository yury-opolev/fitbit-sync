namespace FitbitSync.Domain;

public sealed record MetricFetchRequest(MetricType Metric, IntradayResolution Resolution, DateRange Range);
