using System.Text.Json.Serialization;

namespace FitbitSync.Providers.GoogleHealth;

// Response of GET /v4/users/me/dataTypes/sleep/dataPoints.
internal sealed class GoogleSleepResponse
{
    [JsonPropertyName("dataPoints")]
    public List<GoogleSleepDataPoint> DataPoints { get; set; } = new();

    [JsonPropertyName("nextPageToken")]
    public string? NextPageToken { get; set; }
}
