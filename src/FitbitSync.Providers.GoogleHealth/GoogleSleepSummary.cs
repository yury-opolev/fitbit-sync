using System.Text.Json.Serialization;

namespace FitbitSync.Providers.GoogleHealth;

// The sleep session summary. minutesAsleep is the total minutes asleep (int64-as-string).
internal sealed class GoogleSleepSummary
{
    [JsonPropertyName("minutesAsleep")]
    public string? MinutesAsleep { get; set; }

    [JsonPropertyName("minutesAwake")]
    public string? MinutesAwake { get; set; }

    [JsonPropertyName("minutesInSleepPeriod")]
    public string? MinutesInSleepPeriod { get; set; }

    // Per-stage rollup: one entry per stage type (DEEP/LIGHT/REM/AWAKE/...), each with its total minutes.
    // Already delivered on every sleep dataPoint; the stage mappers index into this by type.
    [JsonPropertyName("stagesSummary")]
    public List<GoogleStageSummary>? StagesSummary { get; set; }
}
