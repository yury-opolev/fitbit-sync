using System.Text.Json.Serialization;

namespace FitbitSync.Providers.GoogleHealth;

// Response of GET /v4/users/me/dataTypes/vo2-max/dataPoints.
internal sealed class GoogleVo2MaxResponse
{
    [JsonPropertyName("dataPoints")]
    public List<GoogleVo2MaxDataPoint> DataPoints { get; set; } = new();

    [JsonPropertyName("nextPageToken")]
    public string? NextPageToken { get; set; }
}
