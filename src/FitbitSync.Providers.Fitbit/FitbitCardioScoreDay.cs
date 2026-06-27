using System.Text.Json.Serialization;

namespace FitbitSync.Providers.Fitbit;

internal sealed record FitbitCardioScoreDay(
    [property: JsonPropertyName("dateTime")] DateOnly DateTime,
    [property: JsonPropertyName("value")] FitbitCardioScoreValue Value);
