using System.Text.Json.Serialization;

namespace FitbitSync.Providers.Fitbit;

internal sealed record FitbitHeartRateDay(
    [property: JsonPropertyName("dateTime")] DateOnly DateTime,
    [property: JsonPropertyName("value")] FitbitHeartRateDayValue Value);
