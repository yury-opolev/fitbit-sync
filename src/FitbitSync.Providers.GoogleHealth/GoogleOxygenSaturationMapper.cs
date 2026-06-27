using FitbitSync.Domain;

namespace FitbitSync.Providers.GoogleHealth;

// Projects "oxygen-saturation" samples into domain MetricSamples (SpO2 percentage at the sample time).
internal static class GoogleOxygenSaturationMapper
{
    private const string Unit = "%";

    public static IReadOnlyList<MetricSample> Map(GoogleOxygenSaturationResponse response, IntradayResolution resolution)
    {
        ArgumentNullException.ThrowIfNull(response);

        var samples = new List<MetricSample>();

        foreach (var point in response.DataPoints)
        {
            if (point.OxygenSaturation?.SampleTime?.PhysicalTime is not { } timestamp)
            {
                continue;
            }

            if (point.OxygenSaturation.Percentage is not { } percentage)
            {
                continue;
            }

            samples.Add(new MetricSample(MetricType.SpO2, timestamp, percentage, Unit, resolution, GoogleHealthDataProvider.ProviderKey));
        }

        return samples;
    }
}
