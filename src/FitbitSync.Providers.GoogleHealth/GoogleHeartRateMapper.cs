using FitbitSync.Domain;

namespace FitbitSync.Providers.GoogleHealth;

// Projects "heart-rate" sample data points into domain MetricSamples (bpm at the sample's physical time).
internal static class GoogleHeartRateMapper
{
    private const string Unit = "bpm";

    public static IReadOnlyList<MetricSample> Map(GoogleHeartRateResponse response, IntradayResolution resolution)
    {
        ArgumentNullException.ThrowIfNull(response);

        var samples = new List<MetricSample>();

        foreach (var point in response.DataPoints)
        {
            if (point.HeartRate?.SampleTime?.PhysicalTime is not { } timestamp)
            {
                continue;
            }

            if (!long.TryParse(point.HeartRate.BeatsPerMinute, out var bpm))
            {
                continue;
            }

            samples.Add(new MetricSample(MetricType.HeartRate, timestamp, bpm, Unit, resolution, GoogleHealthDataProvider.ProviderKey));
        }

        return samples;
    }
}
