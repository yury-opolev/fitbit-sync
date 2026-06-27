using System.Text.Json.Serialization;

namespace FitbitSync.Providers.Fitbit;

internal sealed record FitbitStepsResponse(
    [property: JsonPropertyName("activities-steps")] IReadOnlyList<FitbitStepsDay> Days,
    [property: JsonPropertyName("activities-steps-intraday")] FitbitStepsIntraday Intraday);
