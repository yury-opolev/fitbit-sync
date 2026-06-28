using System.Text.Json.Serialization;

namespace FitbitSync.Providers.GoogleHealth;

internal sealed class GoogleActiveEnergyBurnedDataPoint
{
    [JsonPropertyName("dataSource")]
    public GoogleDataSource? DataSource { get; set; }

    [JsonPropertyName("activeEnergyBurned")]
    public GoogleActiveEnergyBurned? ActiveEnergyBurned { get; set; }
}
