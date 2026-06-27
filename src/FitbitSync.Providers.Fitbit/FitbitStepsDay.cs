using System.Text.Json.Serialization;

namespace FitbitSync.Providers.Fitbit;

internal sealed record FitbitStepsDay(
    [property: JsonPropertyName("dateTime")] DateOnly DateTime,
    [property: JsonPropertyName("value")] string Value);
