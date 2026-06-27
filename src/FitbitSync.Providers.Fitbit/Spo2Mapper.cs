using FitbitSync.Domain;

namespace FitbitSync.Providers.Fitbit;

internal static class Spo2Mapper
{
    private const string Unit = "percent";

    public static IReadOnlyList<MetricSample> Map(FitbitSpo2Response response) =>
    [
        new MetricSample(
            MetricType.SpO2,
            new DateTimeOffset(response.DateTime.ToDateTime(TimeOnly.MinValue), TimeSpan.Zero),
            response.Value.Avg,
            Unit,
            IntradayResolution.Daily,
            FitbitHealthDataProvider.ProviderKey),
    ];
}
