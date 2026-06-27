using System.Text.Json.Serialization;

namespace FitbitSync.Providers.Fitbit;

internal sealed record FitbitSleepLog(
    [property: JsonPropertyName("dateOfSleep")] DateOnly DateOfSleep,
    [property: JsonPropertyName("isMainSleep")] bool IsMainSleep,
    [property: JsonPropertyName("minutesAsleep")] int MinutesAsleep);
