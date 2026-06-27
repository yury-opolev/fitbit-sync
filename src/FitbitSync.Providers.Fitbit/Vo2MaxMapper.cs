using FitbitSync.Domain;

namespace FitbitSync.Providers.Fitbit;

internal static class Vo2MaxMapper
{
    private const string Unit = "mlkgmin";

    public static IReadOnlyList<MetricSample> Map(FitbitCardioScoreResponse response) =>
        response.CardioScore
            .Select(day => new MetricSample(
                MetricType.VO2Max,
                new DateTimeOffset(day.DateTime.ToDateTime(TimeOnly.MinValue), TimeSpan.Zero),
                day.Value.Vo2Max,
                Unit,
                IntradayResolution.Daily,
                FitbitHealthDataProvider.ProviderKey))
            .ToList();
}
