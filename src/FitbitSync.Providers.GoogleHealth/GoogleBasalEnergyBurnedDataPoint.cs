using System.Text.Json.Serialization;

namespace FitbitSync.Providers.GoogleHealth;

internal sealed class GoogleBasalEnergyBurnedDataPoint
{
    [JsonPropertyName("dataSource")]
    public GoogleDataSource? DataSource { get; set; }

    [JsonPropertyName("basalEnergyBurned")]
    public GoogleBasalEnergyBurned? BasalEnergyBurned { get; set; }
}
