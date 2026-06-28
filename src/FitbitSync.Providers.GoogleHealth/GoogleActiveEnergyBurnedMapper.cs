using FitbitSync.Domain;

namespace FitbitSync.Providers.GoogleHealth;

// Projects "active-energy-burned" intervals into domain MetricSamples (kcal burned in activity, at the
// interval start). kcal is a JSON number (double); points missing an interval or a kcal value are skipped.
internal static class GoogleActiveEnergyBurnedMapper
{
    private const string Unit = "kcal";

    public static IReadOnlyList<MetricSample> Map(GoogleActiveEnergyBurnedResponse response, IntradayResolution resolution)
    {
        ArgumentNullException.ThrowIfNull(response);

        var samples = new List<MetricSample>();

        foreach (var point in response.DataPoints)
        {
            if (point.ActiveEnergyBurned?.Interval?.StartTime is not { } start)
            {
                continue;
            }

            if (point.ActiveEnergyBurned.Kcal is not { } kcal)
            {
                continue;
            }

            samples.Add(new MetricSample(MetricType.ActiveCaloriesBurned, start, kcal, Unit, resolution, GoogleHealthDataProvider.ProviderKey));
        }

        return samples;
    }
}
