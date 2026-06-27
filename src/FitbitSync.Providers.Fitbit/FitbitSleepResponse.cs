using System.Text.Json.Serialization;

namespace FitbitSync.Providers.Fitbit;

internal sealed record FitbitSleepResponse(
    [property: JsonPropertyName("sleep")] IReadOnlyList<FitbitSleepLog> Sleep);
