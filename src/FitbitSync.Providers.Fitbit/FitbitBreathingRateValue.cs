using System.Text.Json.Serialization;

namespace FitbitSync.Providers.Fitbit;

internal sealed record FitbitBreathingRateValue(
    [property: JsonPropertyName("breathingRate")] double BreathingRate);
