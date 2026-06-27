using System.Text.Json.Serialization;

namespace FitbitSync.Providers.Fitbit;

internal sealed record FitbitCardioScoreValue(
    [property: JsonPropertyName("vo2Max")]
    [property: JsonConverter(typeof(Vo2MaxConverter))]
    double Vo2Max);
