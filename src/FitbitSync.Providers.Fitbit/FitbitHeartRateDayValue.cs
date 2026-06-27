using System.Text.Json.Serialization;

namespace FitbitSync.Providers.Fitbit;

internal sealed record FitbitHeartRateDayValue(
    [property: JsonPropertyName("restingHeartRate")] int? RestingHeartRate);
