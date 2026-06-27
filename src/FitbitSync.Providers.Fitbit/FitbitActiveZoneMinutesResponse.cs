using System.Text.Json.Serialization;

namespace FitbitSync.Providers.Fitbit;

internal sealed record FitbitActiveZoneMinutesResponse(
    [property: JsonPropertyName("activities-active-zone-minutes")] IReadOnlyList<FitbitActiveZoneMinutesDay> Days);
