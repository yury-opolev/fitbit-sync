using System.Text.Json.Serialization;

namespace FitbitSync.Providers.Fitbit;

internal sealed record FitbitActiveZoneMinutesDay(
    [property: JsonPropertyName("dateTime")] DateOnly DateTime,
    [property: JsonPropertyName("value")] FitbitActiveZoneMinutesValue Value);
