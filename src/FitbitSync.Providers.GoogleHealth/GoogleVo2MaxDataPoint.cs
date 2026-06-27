using System.Text.Json.Serialization;

namespace FitbitSync.Providers.GoogleHealth;

internal sealed class GoogleVo2MaxDataPoint
{
    [JsonPropertyName("dataSource")]
    public GoogleDataSource? DataSource { get; set; }

    [JsonPropertyName("vo2Max")]
    public GoogleVo2Max? Vo2Max { get; set; }
}
