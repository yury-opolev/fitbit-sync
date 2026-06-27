using System.Text.Json.Serialization;

namespace FitbitSync.Providers.Fitbit;

internal sealed record FitbitCardioScoreResponse(
    [property: JsonPropertyName("cardioScore")] IReadOnlyList<FitbitCardioScoreDay> CardioScore);
