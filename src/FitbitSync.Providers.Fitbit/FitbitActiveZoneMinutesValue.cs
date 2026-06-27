using System.Text.Json.Serialization;

namespace FitbitSync.Providers.Fitbit;

internal sealed record FitbitActiveZoneMinutesValue(
    [property: JsonPropertyName("activeZoneMinutes")] int ActiveZoneMinutes,
    [property: JsonPropertyName("fatBurnActiveZoneMinutes")] int FatBurnActiveZoneMinutes,
    [property: JsonPropertyName("cardioActiveZoneMinutes")] int CardioActiveZoneMinutes,
    [property: JsonPropertyName("peakActiveZoneMinutes")] int PeakActiveZoneMinutes);
