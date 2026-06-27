using FitbitSync.Domain;

namespace FitbitSync.Providers.GoogleHealth;

// Projects "heart-rate-variability" samples into domain MetricSamples (RMSSD in milliseconds).
internal static class GoogleHeartRateVariabilityMapper
{
    private const string Unit = "ms";

    public static IReadOnlyList<MetricSample> Map(GoogleHeartRateVariabilityResponse response, IntradayResolution resolution)
    {
        ArgumentNullException.ThrowIfNull(response);

        var samples = new List<MetricSample>();

        foreach (var point in response.DataPoints)
        {
            if (point.HeartRateVariability?.SampleTime?.PhysicalTime is not { } timestamp)
            {
                continue;
            }

            if (point.HeartRateVariability.RmssdMilliseconds is not { } rmssd)
            {
                continue;
            }

            samples.Add(new MetricSample(MetricType.Hrv, timestamp, rmssd, Unit, resolution, GoogleHealthDataProvider.ProviderKey));
        }

        return samples;
    }
}
