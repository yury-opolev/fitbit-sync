using System.Text.Json.Serialization;

namespace FitbitSync.Providers.Fitbit;

internal sealed record FitbitSpo2Value(
    [property: JsonPropertyName("avg")] double Avg,
    [property: JsonPropertyName("min")] double Min,
    [property: JsonPropertyName("max")] double Max);
