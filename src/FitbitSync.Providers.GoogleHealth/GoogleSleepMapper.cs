using FitbitSync.Domain;

namespace FitbitSync.Providers.GoogleHealth;

// Projects each "sleep" session into one domain MetricSample: total minutes asleep, timestamped at the
// session start. Sessions missing an interval or a parseable minutesAsleep are skipped.
internal static class GoogleSleepMapper
{
    private const string Unit = "minutes";

    public static IReadOnlyList<MetricSample> Map(GoogleSleepResponse response, IntradayResolution resolution)
    {
        ArgumentNullException.ThrowIfNull(response);

        var samples = new List<MetricSample>();

        foreach (var point in response.DataPoints)
        {
            if (point.Sleep?.Interval?.StartTime is not { } start)
            {
                continue;
            }

            if (!long.TryParse(point.Sleep.Summary?.MinutesAsleep, out var minutesAsleep))
            {
                continue;
            }

            samples.Add(new MetricSample(MetricType.Sleep, start, minutesAsleep, Unit, resolution, GoogleHealthDataProvider.ProviderKey));
        }

        return samples;
    }
}
