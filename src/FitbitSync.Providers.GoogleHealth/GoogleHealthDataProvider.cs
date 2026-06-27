using FitbitSync.Domain;

namespace FitbitSync.Providers.GoogleHealth;

// Google Health implementation of IHealthDataProvider. Fetches each requested day from
// /v4/users/me/dataTypes/{dataType}/dataPoints (civil-date filtered), then maps the typed payloads to
// domain MetricSamples. Capabilities advertise only the metrics with a confirmed Google mapping.
public sealed class GoogleHealthDataProvider : IHealthDataProvider
{
    public const string ProviderKey = "google";

    private static readonly IReadOnlyList<MetricCapability> CapabilityList =
    [
        new(MetricType.Steps, IntradayResolution.OneMinute),
        new(MetricType.HeartRate, IntradayResolution.OneMinute),
        new(MetricType.Sleep, IntradayResolution.Daily),
    ];

    private readonly GoogleHealthApiClient apiClient;

    public GoogleHealthDataProvider(GoogleHealthApiClient apiClient)
    {
        ArgumentNullException.ThrowIfNull(apiClient);
        this.apiClient = apiClient;
    }

    public string Source => ProviderKey;

    public IReadOnlyList<MetricCapability> Capabilities => CapabilityList;

    public async Task<MetricFetchResult> FetchAsync(MetricFetchRequest request, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        var descriptor = GoogleHealthDataTypeCatalog.Resolve(request.Metric);
        var samples = new List<MetricSample>();

        for (var date = request.Range.Start; date <= request.Range.End; date = date.AddDays(1))
        {
            var url = BuildUrl(descriptor, date);
            samples.AddRange(await this.FetchDayAsync(request.Metric, descriptor, url, ct).ConfigureAwait(false));
        }

        return new MetricFetchResult(samples, null);
    }

    private static string BuildUrl(GoogleDataTypeDescriptor descriptor, DateOnly date)
    {
        var start = date.ToString("yyyy-MM-dd");
        var end = date.AddDays(1).ToString("yyyy-MM-dd");
        var filter = $"{descriptor.FilterMember} >= \"{start}\" AND {descriptor.FilterMember} < \"{end}\"";
        return $"v4/users/me/dataTypes/{descriptor.DataType}/dataPoints?pageSize=10000&filter={Uri.EscapeDataString(filter)}";
    }

    private async Task<IReadOnlyList<MetricSample>> FetchDayAsync(MetricType metric, GoogleDataTypeDescriptor descriptor, string url, CancellationToken ct) =>
        metric switch
        {
            MetricType.Steps => GoogleStepsMapper.Map(await this.apiClient.GetJsonAsync<GoogleStepsResponse>(url, ct).ConfigureAwait(false), descriptor.Resolution),
            MetricType.HeartRate => GoogleHeartRateMapper.Map(await this.apiClient.GetJsonAsync<GoogleHeartRateResponse>(url, ct).ConfigureAwait(false), descriptor.Resolution),
            MetricType.Sleep => GoogleSleepMapper.Map(await this.apiClient.GetJsonAsync<GoogleSleepResponse>(url, ct).ConfigureAwait(false), descriptor.Resolution),
            _ => throw new NotSupportedException($"Google Health provider does not support metric '{metric}'."),
        };
}
