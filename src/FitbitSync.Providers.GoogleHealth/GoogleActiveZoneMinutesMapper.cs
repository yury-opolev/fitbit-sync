using FitbitSync.Domain;

namespace FitbitSync.Providers.GoogleHealth;

// Projects "active-zone-minutes" intervals into domain MetricSamples (AZM earned, at the interval start).
internal static class GoogleActiveZoneMinutesMapper
{
    private const string Unit = "minutes";

    public static IReadOnlyList<MetricSample> Map(GoogleActiveZoneMinutesResponse response, IntradayResolution resolution)
    {
        ArgumentNullException.ThrowIfNull(response);

        var samples = new List<MetricSample>();

        foreach (var point in response.DataPoints)
        {
            if (point.ActiveZoneMinutes?.Interval?.StartTime is not { } start)
            {
                continue;
            }

            if (!long.TryParse(point.ActiveZoneMinutes.ActiveZoneMinutes, out var minutes))
            {
                continue;
            }

            samples.Add(new MetricSample(MetricType.ActiveZoneMinutes, start, minutes, Unit, resolution, GoogleHealthDataProvider.ProviderKey));
        }

        return samples;
    }
}
