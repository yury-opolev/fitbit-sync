using FitbitSync.Domain;

namespace FitbitSync.Providers.GoogleHealth;

// Projects "basal-energy-burned" intervals into domain MetricSamples (resting/BMR kcal, at the interval
// start). kcal is a JSON number (double); points missing an interval or a kcal value are skipped.
internal static class GoogleBasalEnergyBurnedMapper
{
    private const string Unit = "kcal";

    public static IReadOnlyList<MetricSample> Map(GoogleBasalEnergyBurnedResponse response, IntradayResolution resolution)
    {
        ArgumentNullException.ThrowIfNull(response);

        var samples = new List<MetricSample>();

        foreach (var point in response.DataPoints)
        {
            if (point.BasalEnergyBurned?.Interval?.StartTime is not { } start)
            {
                continue;
            }

            if (point.BasalEnergyBurned.Kcal is not { } kcal)
            {
                continue;
            }

            samples.Add(new MetricSample(MetricType.BasalCaloriesBurned, start, kcal, Unit, resolution, GoogleHealthDataProvider.ProviderKey));
        }

        return samples;
    }
}
