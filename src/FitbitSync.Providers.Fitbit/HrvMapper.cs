using FitbitSync.Domain;

namespace FitbitSync.Providers.Fitbit;

internal static class HrvMapper
{
    private const string Unit = "ms";

    public static IReadOnlyList<MetricSample> Map(FitbitHrvResponse response) =>
        response.Hrv
            .Select(day => new MetricSample(
                MetricType.Hrv,
                new DateTimeOffset(day.DateTime.ToDateTime(TimeOnly.MinValue), TimeSpan.Zero),
                day.Value.DailyRmssd,
                Unit,
                IntradayResolution.Daily,
                FitbitHealthDataProvider.ProviderKey))
            .ToList();
}
