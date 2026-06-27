using System.Text.Json.Serialization;

namespace FitbitSync.Providers.GoogleHealth;

// The observed interval of an interval-type data point (steps, active-zone-minutes, distance, ...).
internal sealed class GoogleObservationTimeInterval
{
    [JsonPropertyName("startTime")]
    public DateTimeOffset? StartTime { get; set; }

    [JsonPropertyName("endTime")]
    public DateTimeOffset? EndTime { get; set; }
}
