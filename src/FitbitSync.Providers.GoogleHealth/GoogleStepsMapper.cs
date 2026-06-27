using FitbitSync.Domain;

namespace FitbitSync.Providers.GoogleHealth;

// Projects "steps" data points into domain MetricSamples (one per recorded interval, timestamped at the
// interval start). count is an int64 serialized as a string; points missing an interval or a parseable
// count are skipped rather than faked.
internal static class GoogleStepsMapper
{
    private const string Unit = "steps";

    public static IReadOnlyList<MetricSample> Map(GoogleStepsResponse response, IntradayResolution resolution)
    {
        ArgumentNullException.ThrowIfNull(response);

        var samples = new List<MetricSample>();

        foreach (var point in response.DataPoints)
        {
            if (point.Steps?.Interval?.StartTime is not { } start)
            {
                continue;
            }

            if (!long.TryParse(point.Steps.Count, out var count))
            {
                continue;
            }

            samples.Add(new MetricSample(MetricType.Steps, start, count, Unit, resolution, GoogleHealthDataProvider.ProviderKey));
        }

        return samples;
    }
}
