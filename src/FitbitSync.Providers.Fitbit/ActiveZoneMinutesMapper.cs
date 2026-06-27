using FitbitSync.Domain;

namespace FitbitSync.Providers.Fitbit;

internal static class ActiveZoneMinutesMapper
{
    private const string Unit = "minutes";

    public static IReadOnlyList<MetricSample> Map(FitbitActiveZoneMinutesResponse response) =>
        response.Days
            .Select(day => new MetricSample(
                MetricType.ActiveZoneMinutes,
                new DateTimeOffset(day.DateTime.ToDateTime(TimeOnly.MinValue), TimeSpan.Zero),
                day.Value.ActiveZoneMinutes,
                Unit,
                IntradayResolution.Daily,
                FitbitHealthDataProvider.ProviderKey))
            .ToList();
}
