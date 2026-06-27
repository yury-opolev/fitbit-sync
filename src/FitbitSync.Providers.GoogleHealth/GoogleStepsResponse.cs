using System.Text.Json.Serialization;

namespace FitbitSync.Providers.GoogleHealth;

// Response of GET /v4/users/me/dataTypes/steps/dataPoints.
internal sealed class GoogleStepsResponse
{
    [JsonPropertyName("dataPoints")]
    public List<GoogleStepsDataPoint> DataPoints { get; set; } = new();

    [JsonPropertyName("nextPageToken")]
    public string? NextPageToken { get; set; }
}
