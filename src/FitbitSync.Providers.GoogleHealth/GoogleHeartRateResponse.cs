using System.Text.Json.Serialization;

namespace FitbitSync.Providers.GoogleHealth;

// Response of GET /v4/users/me/dataTypes/heart-rate/dataPoints.
internal sealed class GoogleHeartRateResponse
{
    [JsonPropertyName("dataPoints")]
    public List<GoogleHeartRateDataPoint> DataPoints { get; set; } = new();

    [JsonPropertyName("nextPageToken")]
    public string? NextPageToken { get; set; }
}
