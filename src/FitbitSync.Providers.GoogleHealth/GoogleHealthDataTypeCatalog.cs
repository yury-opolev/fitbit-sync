using FitbitSync.Domain;

namespace FitbitSync.Providers.GoogleHealth;

// Maps domain MetricTypes to Google Health dataType descriptors. dataType ids are the kebab-case of the
// DataPoint union field (e.g. heartRate -> "heart-rate"); the filter member uses the snake_case data-type
// name plus the time path (interval types: .interval.civil_start_time; sample types: .sample_time.civil_time).
// The filter member MUST be snake_case — Google's AIP-160 filter language rejects camelCase segments with
// 400 INVALID_DATA_POINT_FILTER ("Restriction member path segment '<camelCase>' does not match any data type").
// Only metrics with a confirmed, listable Google equivalent are registered — unmapped metrics throw, so
// the provider advertises exactly what works.
internal static class GoogleHealthDataTypeCatalog
{
    private static readonly IReadOnlyDictionary<MetricType, GoogleDataTypeDescriptor> Descriptors =
        new Dictionary<MetricType, GoogleDataTypeDescriptor>
        {
            [MetricType.Steps] = new("steps", "steps.interval.civil_start_time", IntradayResolution.OneMinute),
            [MetricType.HeartRate] = new("heart-rate", "heart_rate.sample_time.civil_time", IntradayResolution.OneMinute),
            [MetricType.Sleep] = new("sleep", "sleep.interval.civil_end_time", IntradayResolution.Daily),
            [MetricType.SpO2] = new("oxygen-saturation", "oxygen_saturation.sample_time.civil_time", IntradayResolution.OneMinute),
            [MetricType.Hrv] = new("heart-rate-variability", "heart_rate_variability.sample_time.civil_time", IntradayResolution.OneMinute),
            [MetricType.ActiveZoneMinutes] = new("active-zone-minutes", "active_zone_minutes.interval.civil_start_time", IntradayResolution.OneMinute),
            [MetricType.VO2Max] = new("vo2-max", "vo2_max.sample_time.civil_time", IntradayResolution.Daily),
            [MetricType.ActiveCaloriesBurned] = new("active-energy-burned", "active_energy_burned.interval.civil_start_time", IntradayResolution.OneMinute),
            [MetricType.BasalCaloriesBurned] = new("basal-energy-burned", "basal_energy_burned.interval.civil_start_time", IntradayResolution.OneMinute),

            // Sleep-stage minute metrics all derive from the SAME "sleep" session dataPoint (stage detail
            // lives in sleep.summary.stagesSummary[]). They reuse the sleep descriptor: same dataType, same
            // end-time filter member. The stage mapper selects the per-stage entry by type.
            [MetricType.SleepDeepMinutes] = new("sleep", "sleep.interval.civil_end_time", IntradayResolution.Daily),
            [MetricType.SleepLightMinutes] = new("sleep", "sleep.interval.civil_end_time", IntradayResolution.Daily),
            [MetricType.SleepRemMinutes] = new("sleep", "sleep.interval.civil_end_time", IntradayResolution.Daily),
            [MetricType.SleepAwakeMinutes] = new("sleep", "sleep.interval.civil_end_time", IntradayResolution.Daily),
        };

    public static GoogleDataTypeDescriptor Resolve(MetricType metric) =>
        Descriptors.TryGetValue(metric, out var descriptor)
            ? descriptor
            : throw new NotSupportedException($"Google Health provider has no data-type mapping for metric '{metric}'.");

    public static bool IsSupported(MetricType metric) => Descriptors.ContainsKey(metric);
}
