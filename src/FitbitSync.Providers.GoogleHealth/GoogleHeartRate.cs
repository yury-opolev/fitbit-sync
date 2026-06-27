using System.Text.Json.Serialization;

namespace FitbitSync.Providers.GoogleHealth;

// The "heart-rate" sample payload: a bpm reading at an observation time. beatsPerMinute is int64-as-string.
internal sealed class GoogleHeartRate
{
    [JsonPropertyName("sampleTime")]
    public GoogleObservationSampleTime? SampleTime { get; set; }

    [JsonPropertyName("beatsPerMinute")]
    public string? BeatsPerMinute { get; set; }
}
