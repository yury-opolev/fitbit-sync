using System.Text.Json.Serialization;

namespace FitbitSync.Providers.GoogleHealth;

// One per-stage rollup entry inside sleep.summary.stagesSummary[]. type is a bare UPPER_SNAKE stage value
// (AWAKE/LIGHT/DEEP/REM/ASLEEP/RESTLESS); minutes and count are int64 serialized as strings.
internal sealed class GoogleStageSummary
{
    [JsonPropertyName("type")]
    public string? Type { get; set; }

    [JsonPropertyName("minutes")]
    public string? Minutes { get; set; }

    [JsonPropertyName("count")]
    public string? Count { get; set; }
}
