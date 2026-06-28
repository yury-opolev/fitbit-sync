using FitbitSync.Domain;

namespace FitbitSync.Providers.GoogleHealth;

// Projects per-stage sleep durations out of the SAME "sleep" dataPoint we already fetch. The Google Health
// API delivers stage detail in sleep.summary.stagesSummary[] (one entry per stage type, with minutes), so
// this is a deserialization-only extraction — no extra dataType. One night yields one sample of the
// requested stage's minutes, timestamped at the session start. Nights without a matching stage entry
// (e.g. naps, or CLASSIC nights with no DEEP/LIGHT/REM) are skipped rather than emitted as zero.
internal static class GoogleSleepStageMapper
{
    private const string Unit = "minutes";

    public static IReadOnlyList<MetricSample> Map(
        GoogleSleepResponse response,
        IntradayResolution resolution,
        string stageType,
        MetricType metricType)
    {
        ArgumentNullException.ThrowIfNull(response);
        ArgumentException.ThrowIfNullOrEmpty(stageType);

        var samples = new List<MetricSample>();

        foreach (var point in response.DataPoints)
        {
            if (point.Sleep?.Interval?.StartTime is not { } start)
            {
                continue;
            }

            var stagesSummary = point.Sleep.Summary?.StagesSummary;
            if (stagesSummary is null)
            {
                continue;
            }

            var entry = stagesSummary.FirstOrDefault(
                s => string.Equals(s.Type, stageType, StringComparison.OrdinalIgnoreCase));

            if (entry is null || !long.TryParse(entry.Minutes, out var minutes))
            {
                continue;
            }

            samples.Add(new MetricSample(metricType, start, minutes, Unit, resolution, GoogleHealthDataProvider.ProviderKey));
        }

        return samples;
    }
}
