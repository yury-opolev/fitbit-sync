using FitbitSync.Domain;

namespace FitbitSync.Providers.Fitbit;

internal static class StepsMapper
{
    private const string Unit = "steps";

    public static IReadOnlyList<MetricSample> Map(FitbitStepsResponse response, IntradayResolution resolution)
    {
        if (response.Days.Count == 0)
        {
            return [];
        }

        var date = response.Days[0].DateTime;

        return response.Intraday.Dataset
            .Select(point => new MetricSample(
                MetricType.Steps,
                new DateTimeOffset(date.ToDateTime(point.Time), TimeSpan.Zero),
                point.Value,
                Unit,
                resolution,
                FitbitHealthDataProvider.ProviderKey))
            .ToList();
    }
}
