using System.Text.Json.Serialization;

namespace FitbitSync.Providers.GoogleHealth;

// The "oxygen-saturation" sample payload: an instantaneous SpO2 percentage at an observation time.
internal sealed class GoogleOxygenSaturation
{
    [JsonPropertyName("sampleTime")]
    public GoogleObservationSampleTime? SampleTime { get; set; }

    [JsonPropertyName("percentage")]
    public double? Percentage { get; set; }
}
