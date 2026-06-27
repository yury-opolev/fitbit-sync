using System.Text.Json.Serialization;

namespace FitbitSync.Providers.Fitbit;

internal sealed record FitbitBreathingRateResponse(
    [property: JsonPropertyName("br")] IReadOnlyList<FitbitBreathingRateDay> Br);
