using System.Text.Json.Serialization;

namespace FitbitSync.Providers.Fitbit;

internal sealed record FitbitStepsIntraday(
    [property: JsonPropertyName("dataset")] IReadOnlyList<FitbitStepsIntradayPoint> Dataset,
    [property: JsonPropertyName("datasetInterval")] int DatasetInterval,
    [property: JsonPropertyName("datasetType")] string DatasetType);
