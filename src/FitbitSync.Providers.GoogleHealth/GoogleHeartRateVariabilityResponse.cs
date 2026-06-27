using System.Text.Json.Serialization;

namespace FitbitSync.Providers.GoogleHealth;

// Response of GET /v4/users/me/dataTypes/heart-rate-variability/dataPoints.
internal sealed class GoogleHeartRateVariabilityResponse
{
    [JsonPropertyName("dataPoints")]
    public List<GoogleHeartRateVariabilityDataPoint> DataPoints { get; set; } = new();

    [JsonPropertyName("nextPageToken")]
    public string? NextPageToken { get; set; }
}
