using System.Text.Json.Serialization;

namespace FitbitSync.Providers.GoogleHealth;

internal sealed class GoogleHeartRateVariabilityDataPoint
{
    [JsonPropertyName("dataSource")]
    public GoogleDataSource? DataSource { get; set; }

    [JsonPropertyName("heartRateVariability")]
    public GoogleHeartRateVariability? HeartRateVariability { get; set; }
}
