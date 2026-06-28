using System.Text.Json.Serialization;

namespace FitbitSync.Providers.GoogleHealth;

// Response of GET /v4/users/me/dataTypes/basal-energy-burned/dataPoints.
internal sealed class GoogleBasalEnergyBurnedResponse
{
    [JsonPropertyName("dataPoints")]
    public List<GoogleBasalEnergyBurnedDataPoint> DataPoints { get; set; } = new();

    [JsonPropertyName("nextPageToken")]
    public string? NextPageToken { get; set; }
}
