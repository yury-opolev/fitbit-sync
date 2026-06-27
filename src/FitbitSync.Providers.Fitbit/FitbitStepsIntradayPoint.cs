using System.Text.Json.Serialization;

namespace FitbitSync.Providers.Fitbit;

internal sealed record FitbitStepsIntradayPoint(
    [property: JsonPropertyName("time")] TimeOnly Time,
    [property: JsonPropertyName("value")] double Value);
