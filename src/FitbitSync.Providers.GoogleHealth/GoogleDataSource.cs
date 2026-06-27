using System.Text.Json.Serialization;

namespace FitbitSync.Providers.GoogleHealth;

internal sealed class GoogleDataSource
{
    [JsonPropertyName("platform")]
    public string? Platform { get; set; }

    [JsonPropertyName("recordingMethod")]
    public string? RecordingMethod { get; set; }

    [JsonPropertyName("device")]
    public GoogleDevice? Device { get; set; }
}
