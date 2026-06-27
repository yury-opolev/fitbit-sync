using FitbitSync.Domain;
using FitbitSync.Providers.Fitbit;
using FluentAssertions;

namespace FitbitSync.Providers.Fitbit.Tests;

// Phase 3 red test: the endpoint catalog maps a (metric, resolution) pair plus a date to the
// documented Fitbit Web API relative URL (base address is owned by the typed client). Heart-rate
// intraday encodes its detail level; daily metrics use their date endpoints; unsupported pairs throw.
public sealed class EndpointCatalogTests
{
    private static readonly DateOnly Date = new(2024, 5, 10);

    [Fact]
    public void EndpointCatalog_BuildsHeartRateIntradayUrl_ForDate()
    {
        FitbitEndpointCatalog.Resolve(MetricType.HeartRate, IntradayResolution.OneMinute, Date)
            .RelativePath.Should().Be("activities/heart/date/2024-05-10/1d/1min.json");
    }

    [Fact]
    public void EndpointCatalog_BuildsDailyMetricUrl_ForDate()
    {
        FitbitEndpointCatalog.Resolve(MetricType.SpO2, IntradayResolution.Daily, Date)
            .RelativePath.Should().Be("spo2/date/2024-05-10.json");
    }

    [Fact]
    public void EndpointCatalog_BuildsSleepUrl_ForDate()
    {
        FitbitEndpointCatalog.Resolve(MetricType.Sleep, IntradayResolution.Daily, Date)
            .RelativePath.Should().Be("sleep/date/2024-05-10.json");
    }

    [Fact]
    public void EndpointCatalog_Throws_ForUnsupportedPair()
    {
        // SpO2 is a daily-only metric; requesting an intraday resolution for it has no endpoint.
        var resolve = () => FitbitEndpointCatalog.Resolve(MetricType.SpO2, IntradayResolution.OneSecond, Date);
        resolve.Should().Throw<NotSupportedException>();
    }
}
