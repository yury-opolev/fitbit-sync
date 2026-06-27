using System.Globalization;
using FitbitSync.Domain;

namespace FitbitSync.Providers.Fitbit;

internal static class FitbitEndpointCatalog
{
    public static EndpointDescriptor Resolve(MetricType metric, IntradayResolution resolution, DateOnly date)
    {
        var day = date.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);

        var path = (metric, resolution) switch
        {
            (MetricType.HeartRate, _) => $"activities/heart/date/{day}/1d/{HeartRateDetail(resolution)}.json",
            (MetricType.Steps, _) => $"activities/steps/date/{day}/1d.json",
            (MetricType.Sleep, IntradayResolution.Daily) => $"sleep/date/{day}.json",
            (MetricType.SpO2, IntradayResolution.Daily) => $"spo2/date/{day}.json",
            (MetricType.BreathingRate, IntradayResolution.Daily) => $"br/date/{day}.json",
            (MetricType.Hrv, IntradayResolution.Daily) => $"hrv/date/{day}.json",
            (MetricType.Temperature, IntradayResolution.Daily) => $"temp/skin/date/{day}.json",
            (MetricType.VO2Max, IntradayResolution.Daily) => $"cardioscore/date/{day}.json",
            (MetricType.ActiveZoneMinutes, IntradayResolution.Daily) => $"activities/active-zone-minutes/date/{day}/1d.json",
            _ => throw new NotSupportedException($"No Fitbit endpoint for metric '{metric}' at resolution '{resolution}'."),
        };

        return new EndpointDescriptor(path);
    }

    private static string HeartRateDetail(IntradayResolution resolution) => resolution switch
    {
        IntradayResolution.OneSecond => "1sec",
        IntradayResolution.OneMinute => "1min",
        IntradayResolution.FiveMinute => "5min",
        IntradayResolution.FifteenMinute => "15min",
        _ => throw new NotSupportedException($"Heart-rate intraday does not support resolution '{resolution}'."),
    };
}
