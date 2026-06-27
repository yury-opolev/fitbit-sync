namespace FitbitSync.Domain;

public sealed record MetricFetchResult(IReadOnlyList<MetricSample> Samples, RateLimitSnapshot? RateLimit);
