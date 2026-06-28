using FitbitSync.Domain;

namespace FitbitSync.Providers.GoogleHealth;

// Projects per-stage sleep durations out of the SAME "sleep" dataPoint we already fetch. The Google Health
// API delivers stage detail in sleep.summary.stagesSummary[] (one entry per stage type, with minutes), so
// this is a deserialization-only extraction — no extra dataType. One night yields one sample of the
// requested stage's minutes, timestamped at the session start. Nights without a matching stage entry
// (e.g. naps, or CLASSIC nights with no DEEP/LIGHT/REM) are skipped rather than emitted as zero.
//
// The Google stage literal (DEEP/LIGHT/REM/AWAKE) is DERIVED from the sleep-stage MetricType here, not
// passed in by the caller. That makes a stage<->metric mismatch (e.g. a copy-paste DEEP/LIGHT swap at a
// call site) unrepresentable: a SleepXxxMinutes metric can only ever read its own stage.
internal static class GoogleSleepStageMapper
{
    private const string Unit = "minutes";

    // The single source of truth for which Google stage literal each sleep-stage metric reads.
    private static readonly IReadOnlyDictionary<MetricType, string> StageTypeByMetric =
        new Dictionary<MetricType, string>
        {
            [MetricType.SleepDeepMinutes] = "DEEP",
            [MetricType.SleepLightMinutes] = "LIGHT",
            [MetricType.SleepRemMinutes] = "REM",
            [MetricType.SleepAwakeMinutes] = "AWAKE",
        };

    public static IReadOnlyList<MetricSample> Map(
        GoogleSleepResponse response,
        IntradayResolution resolution,
        MetricType metricType)
    {
        ArgumentNullException.ThrowIfNull(response);

        var stageType = StageTypeFor(metricType);
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

    private static string StageTypeFor(MetricType metricType) =>
        StageTypeByMetric.TryGetValue(metricType, out var stageType)
            ? stageType
            : throw new ArgumentOutOfRangeException(
                nameof(metricType), metricType, "Not a sleep-stage metric (expected SleepDeep/Light/Rem/AwakeMinutes).");
}
