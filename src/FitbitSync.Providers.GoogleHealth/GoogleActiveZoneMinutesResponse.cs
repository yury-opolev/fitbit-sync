using System.Text.Json.Serialization;

namespace FitbitSync.Providers.GoogleHealth;

// Response of GET /v4/users/me/dataTypes/active-zone-minutes/dataPoints.
internal sealed class GoogleActiveZoneMinutesResponse
{
    [JsonPropertyName("dataPoints")]
    public List<GoogleActiveZoneMinutesDataPoint> DataPoints { get; set; } = new();

    [JsonPropertyName("nextPageToken")]
    public string? NextPageToken { get; set; }
}
