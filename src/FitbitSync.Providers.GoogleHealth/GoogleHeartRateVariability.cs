using System.Text.Json.Serialization;

namespace FitbitSync.Providers.GoogleHealth;

// The "heart-rate-variability" sample payload. RMSSD (root mean square of successive differences) in
// milliseconds is the value Google Health surfaces for HRV.
internal sealed class GoogleHeartRateVariability
{
    [JsonPropertyName("sampleTime")]
    public GoogleObservationSampleTime? SampleTime { get; set; }

    [JsonPropertyName("rootMeanSquareOfSuccessiveDifferencesMilliseconds")]
    public double? RmssdMilliseconds { get; set; }
}
