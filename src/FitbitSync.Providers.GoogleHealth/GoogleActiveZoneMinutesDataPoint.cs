using System.Text.Json.Serialization;

namespace FitbitSync.Providers.GoogleHealth;

internal sealed class GoogleActiveZoneMinutesDataPoint
{
    [JsonPropertyName("dataSource")]
    public GoogleDataSource? DataSource { get; set; }

    [JsonPropertyName("activeZoneMinutes")]
    public GoogleActiveZoneMinutes? ActiveZoneMinutes { get; set; }
}
