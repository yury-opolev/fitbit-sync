using System.Text.Json.Serialization;

namespace FitbitSync.Providers.GoogleHealth;

// Response of GET /v4/users/me/dataTypes/active-energy-burned/dataPoints.
internal sealed class GoogleActiveEnergyBurnedResponse
{
    [JsonPropertyName("dataPoints")]
    public List<GoogleActiveEnergyBurnedDataPoint> DataPoints { get; set; } = new();

    [JsonPropertyName("nextPageToken")]
    public string? NextPageToken { get; set; }
}
