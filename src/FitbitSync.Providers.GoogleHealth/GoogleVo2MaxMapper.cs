using FitbitSync.Domain;

namespace FitbitSync.Providers.GoogleHealth;

// Projects "vo2-max" samples into domain MetricSamples (VO2 max in mL/kg/min at the sample time).
internal static class GoogleVo2MaxMapper
{
    private const string Unit = "mL/kg/min";

    public static IReadOnlyList<MetricSample> Map(GoogleVo2MaxResponse response, IntradayResolution resolution)
    {
        ArgumentNullException.ThrowIfNull(response);

        var samples = new List<MetricSample>();

        foreach (var point in response.DataPoints)
        {
            if (point.Vo2Max?.SampleTime?.PhysicalTime is not { } timestamp)
            {
                continue;
            }

            if (point.Vo2Max.Vo2Max is not { } value)
            {
                continue;
            }

            samples.Add(new MetricSample(MetricType.VO2Max, timestamp, value, Unit, resolution, GoogleHealthDataProvider.ProviderKey));
        }

        return samples;
    }
}
