using System.Text.Json.Serialization;

namespace FitbitSync.Providers.GoogleHealth;

// The "active-zone-minutes" interval payload: AZM earned in the interval (int64-as-string; 1 per minute in
// fat-burn zones, 2 per minute in cardio/peak zones).
internal sealed class GoogleActiveZoneMinutes
{
    [JsonPropertyName("interval")]
    public GoogleObservationTimeInterval? Interval { get; set; }

    [JsonPropertyName("activeZoneMinutes")]
    public string? ActiveZoneMinutes { get; set; }
}
