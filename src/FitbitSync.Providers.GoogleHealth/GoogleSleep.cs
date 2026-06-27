using System.Text.Json.Serialization;

namespace FitbitSync.Providers.GoogleHealth;

// The "sleep" session payload. The session interval shares the start/end shape of an observation interval,
// so it is deserialized into GoogleObservationTimeInterval. summary.minutesAsleep is the mapped value.
internal sealed class GoogleSleep
{
    [JsonPropertyName("interval")]
    public GoogleObservationTimeInterval? Interval { get; set; }

    [JsonPropertyName("summary")]
    public GoogleSleepSummary? Summary { get; set; }
}
