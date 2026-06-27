using System.Text.Json.Serialization;

namespace FitbitSync.Providers.Fitbit;

internal sealed record FitbitHrvResponse(
    [property: JsonPropertyName("hrv")] IReadOnlyList<FitbitHrvDay> Hrv);
