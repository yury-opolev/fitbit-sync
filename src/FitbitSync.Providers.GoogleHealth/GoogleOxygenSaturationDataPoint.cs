using System.Text.Json.Serialization;

namespace FitbitSync.Providers.GoogleHealth;

internal sealed class GoogleOxygenSaturationDataPoint
{
    [JsonPropertyName("dataSource")]
    public GoogleDataSource? DataSource { get; set; }

    [JsonPropertyName("oxygenSaturation")]
    public GoogleOxygenSaturation? OxygenSaturation { get; set; }
}
