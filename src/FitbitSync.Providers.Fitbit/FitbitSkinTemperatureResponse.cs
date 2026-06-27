using System.Text.Json.Serialization;

namespace FitbitSync.Providers.Fitbit;

internal sealed record FitbitSkinTemperatureResponse(
    [property: JsonPropertyName("tempSkin")] IReadOnlyList<FitbitSkinTemperatureDay> TempSkin);
