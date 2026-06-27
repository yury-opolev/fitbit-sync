using System.Text.Json.Serialization;

namespace FitbitSync.Providers.GoogleHealth;

// The "vo2-max" sample payload: a VO2 max reading (mL O2 / kg / min) at an observation time.
internal sealed class GoogleVo2Max
{
    [JsonPropertyName("sampleTime")]
    public GoogleObservationSampleTime? SampleTime { get; set; }

    [JsonPropertyName("vo2Max")]
    public double? Vo2Max { get; set; }
}
