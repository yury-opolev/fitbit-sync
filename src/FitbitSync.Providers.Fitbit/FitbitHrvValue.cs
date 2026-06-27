using System.Text.Json.Serialization;

namespace FitbitSync.Providers.Fitbit;

internal sealed record FitbitHrvValue(
    [property: JsonPropertyName("dailyRmssd")] double DailyRmssd,
    [property: JsonPropertyName("deepRmssd")] double DeepRmssd);
