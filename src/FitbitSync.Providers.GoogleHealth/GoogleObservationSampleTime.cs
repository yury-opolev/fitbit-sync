using System.Text.Json.Serialization;

namespace FitbitSync.Providers.GoogleHealth;

// The sample time of a sample-type data point (heart rate, oxygen saturation, HRV, VO2 max, ...).
internal sealed class GoogleObservationSampleTime
{
    [JsonPropertyName("physicalTime")]
    public DateTimeOffset? PhysicalTime { get; set; }
}
