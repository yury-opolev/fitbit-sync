using System.Text.Json.Serialization;

namespace FitbitSync.Providers.GoogleHealth;

// The "steps" payload: a step count over an observed interval. count is an int64 serialized as a string.
internal sealed class GoogleSteps
{
    [JsonPropertyName("interval")]
    public GoogleObservationTimeInterval? Interval { get; set; }

    [JsonPropertyName("count")]
    public string? Count { get; set; }
}
