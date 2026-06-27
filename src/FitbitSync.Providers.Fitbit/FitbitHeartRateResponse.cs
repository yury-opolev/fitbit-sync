using System.Text.Json.Serialization;

namespace FitbitSync.Providers.Fitbit;

internal sealed record FitbitHeartRateResponse(
    [property: JsonPropertyName("activities-heart")] IReadOnlyList<FitbitHeartRateDay> Days,
    [property: JsonPropertyName("activities-heart-intraday")] FitbitHeartRateIntraday Intraday);
