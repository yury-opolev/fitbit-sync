using System.Text.Json.Serialization;

namespace FitbitSync.Providers.GoogleHealth;

internal sealed class GoogleStepsDataPoint
{
    [JsonPropertyName("dataSource")]
    public GoogleDataSource? DataSource { get; set; }

    [JsonPropertyName("steps")]
    public GoogleSteps? Steps { get; set; }
}
