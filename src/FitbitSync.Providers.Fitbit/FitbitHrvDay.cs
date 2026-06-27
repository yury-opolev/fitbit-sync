using System.Text.Json.Serialization;

namespace FitbitSync.Providers.Fitbit;

internal sealed record FitbitHrvDay(
    [property: JsonPropertyName("dateTime")] DateOnly DateTime,
    [property: JsonPropertyName("value")] FitbitHrvValue Value);
