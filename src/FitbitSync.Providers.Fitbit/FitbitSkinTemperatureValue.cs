using System.Text.Json.Serialization;

namespace FitbitSync.Providers.Fitbit;

internal sealed record FitbitSkinTemperatureValue(
    [property: JsonPropertyName("nightlyRelative")] double NightlyRelative);
