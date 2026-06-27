using FitbitSync.Domain;

namespace FitbitSync.Providers.Fitbit;

internal static class BreathingRateMapper
{
    private const string Unit = "brpm";

    public static IReadOnlyList<MetricSample> Map(FitbitBreathingRateResponse response) =>
        response.Br
            .Select(day => new MetricSample(
                MetricType.BreathingRate,
                new DateTimeOffset(day.DateTime.ToDateTime(TimeOnly.MinValue), TimeSpan.Zero),
                day.Value.BreathingRate,
                Unit,
                IntradayResolution.Daily,
                FitbitHealthDataProvider.ProviderKey))
            .ToList();
}
