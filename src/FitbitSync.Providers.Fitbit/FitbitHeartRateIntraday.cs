using System.Text.Json.Serialization;

namespace FitbitSync.Providers.Fitbit;

internal sealed record FitbitHeartRateIntraday(
    [property: JsonPropertyName("dataset")] IReadOnlyList<FitbitHeartRateIntradayPoint> Dataset,
    [property: JsonPropertyName("datasetInterval")] int DatasetInterval,
    [property: JsonPropertyName("datasetType")] string DatasetType);
