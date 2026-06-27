using FitbitSync.Domain;

namespace FitbitSync.Providers.GoogleHealth;

// Maps domain MetricTypes to Google Health dataType descriptors. dataType ids are the kebab-case of the
// DataPoint union field (e.g. heartRate -> "heart-rate"); the filter member uses the camelCase field plus
// the time path (interval types: .interval.civil_start_time; sample types: .sample_time.civil_time).
// Only metrics with a confirmed, listable Google equivalent are registered — unmapped metrics throw, so
// the provider advertises exactly what works.
internal static class GoogleHealthDataTypeCatalog
{
    private static readonly IReadOnlyDictionary<MetricType, GoogleDataTypeDescriptor> Descriptors =
        new Dictionary<MetricType, GoogleDataTypeDescriptor>
        {
            [MetricType.Steps] = new("steps", "steps.interval.civil_start_time", IntradayResolution.OneMinute),
            [MetricType.HeartRate] = new("heart-rate", "heartRate.sample_time.civil_time", IntradayResolution.OneMinute),
            [MetricType.Sleep] = new("sleep", "sleep.interval.civil_end_time", IntradayResolution.Daily),
        };

    public static GoogleDataTypeDescriptor Resolve(MetricType metric) =>
        Descriptors.TryGetValue(metric, out var descriptor)
            ? descriptor
            : throw new NotSupportedException($"Google Health provider has no data-type mapping for metric '{metric}'.");

    public static bool IsSupported(MetricType metric) => Descriptors.ContainsKey(metric);
}
