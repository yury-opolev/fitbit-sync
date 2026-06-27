using System.Text.Json.Serialization;

namespace FitbitSync.Providers.GoogleHealth;

internal sealed class GoogleDevice
{
    [JsonPropertyName("displayName")]
    public string? DisplayName { get; set; }
}
