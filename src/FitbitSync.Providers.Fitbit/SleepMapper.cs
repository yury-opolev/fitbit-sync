using FitbitSync.Domain;

namespace FitbitSync.Providers.Fitbit;

internal static class SleepMapper
{
    private const string Unit = "minutes";

    public static IReadOnlyList<MetricSample> Map(FitbitSleepResponse response) =>
        response.Sleep
            .Where(log => log.IsMainSleep)
            .Select(log => new MetricSample(
                MetricType.Sleep,
                new DateTimeOffset(log.DateOfSleep.ToDateTime(TimeOnly.MinValue), TimeSpan.Zero),
                log.MinutesAsleep,
                Unit,
                IntradayResolution.Daily,
                FitbitHealthDataProvider.ProviderKey))
            .ToList();
}
