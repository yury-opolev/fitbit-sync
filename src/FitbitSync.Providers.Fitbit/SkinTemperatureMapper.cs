using FitbitSync.Domain;

namespace FitbitSync.Providers.Fitbit;

internal static class SkinTemperatureMapper
{
    private const string Unit = "celsius";

    public static IReadOnlyList<MetricSample> Map(FitbitSkinTemperatureResponse response) =>
        response.TempSkin
            .Select(day => new MetricSample(
                MetricType.Temperature,
                new DateTimeOffset(day.DateTime.ToDateTime(TimeOnly.MinValue), TimeSpan.Zero),
                day.Value.NightlyRelative,
                Unit,
                IntradayResolution.Daily,
                FitbitHealthDataProvider.ProviderKey))
            .ToList();
}
