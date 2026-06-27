using FitbitSync.Domain;

namespace FitbitSync.Providers.Fitbit;

public sealed class FitbitHealthDataProvider : IHealthDataProvider
{
    public const string ProviderKey = "fitbit";

    private static readonly IReadOnlyList<MetricCapability> capabilities =
    [
        new(MetricType.HeartRate, IntradayResolution.OneMinute),
        new(MetricType.Sleep, IntradayResolution.Daily),
        new(MetricType.SpO2, IntradayResolution.Daily),
        new(MetricType.BreathingRate, IntradayResolution.Daily),
        new(MetricType.Hrv, IntradayResolution.Daily),
        new(MetricType.Temperature, IntradayResolution.Daily),
        new(MetricType.VO2Max, IntradayResolution.Daily),
        new(MetricType.ActiveZoneMinutes, IntradayResolution.Daily),
        new(MetricType.Steps, IntradayResolution.Daily),
    ];

    private readonly FitbitApiClient apiClient;

    public FitbitHealthDataProvider(FitbitApiClient apiClient)
    {
        ArgumentNullException.ThrowIfNull(apiClient);
        this.apiClient = apiClient;
    }

    public string Source => ProviderKey;

    public IReadOnlyList<MetricCapability> Capabilities => capabilities;

    public async Task<MetricFetchResult> FetchAsync(MetricFetchRequest request, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        var samples = new List<MetricSample>();

        for (var date = request.Range.Start; date <= request.Range.End; date = date.AddDays(1))
        {
            var path = FitbitEndpointCatalog.Resolve(request.Metric, request.Resolution, date).RelativePath;
            samples.AddRange(await this.FetchDayAsync(request.Metric, request.Resolution, path, ct).ConfigureAwait(false));
        }

        return new MetricFetchResult(samples, this.apiClient.LatestRateLimit);
    }

    private async Task<IReadOnlyList<MetricSample>> FetchDayAsync(MetricType metric, IntradayResolution resolution, string path, CancellationToken ct) =>
        metric switch
        {
            MetricType.HeartRate => HeartRateMapper.Map(await this.apiClient.GetJsonAsync<FitbitHeartRateResponse>(path, ct).ConfigureAwait(false), resolution),
            MetricType.Steps => StepsMapper.Map(await this.apiClient.GetJsonAsync<FitbitStepsResponse>(path, ct).ConfigureAwait(false), resolution),
            MetricType.Sleep => SleepMapper.Map(await this.apiClient.GetJsonAsync<FitbitSleepResponse>(path, ct).ConfigureAwait(false)),
            MetricType.SpO2 => Spo2Mapper.Map(await this.apiClient.GetJsonAsync<FitbitSpo2Response>(path, ct).ConfigureAwait(false)),
            MetricType.BreathingRate => BreathingRateMapper.Map(await this.apiClient.GetJsonAsync<FitbitBreathingRateResponse>(path, ct).ConfigureAwait(false)),
            MetricType.Hrv => HrvMapper.Map(await this.apiClient.GetJsonAsync<FitbitHrvResponse>(path, ct).ConfigureAwait(false)),
            MetricType.Temperature => SkinTemperatureMapper.Map(await this.apiClient.GetJsonAsync<FitbitSkinTemperatureResponse>(path, ct).ConfigureAwait(false)),
            MetricType.VO2Max => Vo2MaxMapper.Map(await this.apiClient.GetJsonAsync<FitbitCardioScoreResponse>(path, ct).ConfigureAwait(false)),
            MetricType.ActiveZoneMinutes => ActiveZoneMinutesMapper.Map(await this.apiClient.GetJsonAsync<FitbitActiveZoneMinutesResponse>(path, ct).ConfigureAwait(false)),
            _ => throw new NotSupportedException($"Fitbit provider does not support metric '{metric}'."),
        };
}
