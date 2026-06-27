using System.Text.Json;
using FitbitSync.Domain;
using FitbitSync.Providers.Fitbit;
using FluentAssertions;

namespace FitbitSync.Providers.Fitbit.Tests;

// Phase 3 (3d-i): the four daily scalar metrics each map their documented Fitbit daily response to
// exactly one canonical MetricSample — correct type, a midnight-UTC timestamp from the response date,
// the mapped scalar value, the metric's unit, Daily resolution, and source "fitbit". Fixtures mirror
// the documented schemas.
public sealed class DailyScalarMapperTests
{
    private static readonly DateTimeOffset Midnight = new(2024, 5, 10, 0, 0, 0, TimeSpan.Zero);

    [Fact]
    public void FitbitProvider_MapsSpo2Response_ToCanonicalSample()
    {
        const string fixture = """
        { "dateTime": "2024-05-10", "value": { "avg": 95.5, "min": 90.1, "max": 99.0 } }
        """;
        var response = JsonSerializer.Deserialize<FitbitSpo2Response>(fixture)!;

        var samples = Spo2Mapper.Map(response);

        samples.Should().ContainSingle();
        var sample = samples[0];
        sample.Type.Should().Be(MetricType.SpO2);
        sample.Timestamp.Should().Be(Midnight);
        sample.Value.Should().Be(95.5);
        sample.Unit.Should().Be("percent");
        sample.Resolution.Should().Be(IntradayResolution.Daily);
        sample.Source.Should().Be("fitbit");
    }

    [Fact]
    public void FitbitProvider_MapsBreathingRateResponse_ToCanonicalSample()
    {
        const string fixture = """
        { "br": [ { "value": { "breathingRate": 16.4 }, "dateTime": "2024-05-10" } ] }
        """;
        var response = JsonSerializer.Deserialize<FitbitBreathingRateResponse>(fixture)!;

        var samples = BreathingRateMapper.Map(response);

        samples.Should().ContainSingle();
        var sample = samples[0];
        sample.Type.Should().Be(MetricType.BreathingRate);
        sample.Timestamp.Should().Be(Midnight);
        sample.Value.Should().Be(16.4);
        sample.Unit.Should().Be("brpm");
        sample.Resolution.Should().Be(IntradayResolution.Daily);
        sample.Source.Should().Be("fitbit");
    }

    [Fact]
    public void FitbitProvider_MapsHrvResponse_ToCanonicalSample()
    {
        const string fixture = """
        { "hrv": [ { "value": { "dailyRmssd": 34.2, "deepRmssd": 28.9 }, "dateTime": "2024-05-10" } ] }
        """;
        var response = JsonSerializer.Deserialize<FitbitHrvResponse>(fixture)!;

        var samples = HrvMapper.Map(response);

        samples.Should().ContainSingle();
        var sample = samples[0];
        sample.Type.Should().Be(MetricType.Hrv);
        sample.Timestamp.Should().Be(Midnight);
        sample.Value.Should().Be(34.2);
        sample.Unit.Should().Be("ms");
        sample.Resolution.Should().Be(IntradayResolution.Daily);
        sample.Source.Should().Be("fitbit");
    }

    [Fact]
    public void FitbitProvider_MapsTemperatureResponse_ToCanonicalSample()
    {
        const string fixture = """
        { "tempSkin": [ { "value": { "nightlyRelative": -0.3 }, "dateTime": "2024-05-10" } ] }
        """;
        var response = JsonSerializer.Deserialize<FitbitSkinTemperatureResponse>(fixture)!;

        var samples = SkinTemperatureMapper.Map(response);

        samples.Should().ContainSingle();
        var sample = samples[0];
        sample.Type.Should().Be(MetricType.Temperature);
        sample.Timestamp.Should().Be(Midnight);
        sample.Value.Should().Be(-0.3);
        sample.Unit.Should().Be("celsius");
        sample.Resolution.Should().Be(IntradayResolution.Daily);
        sample.Source.Should().Be("fitbit");
    }
}
