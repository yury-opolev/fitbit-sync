using System.Text.Json;
using FitbitSync.Domain;
using FitbitSync.Providers.Fitbit;
using FluentAssertions;

namespace FitbitSync.Providers.Fitbit.Tests;

// Phase 3 (3d-ii): the remaining three metrics. VO2Max parses the average of a range string (or a
// single number); ActiveZoneMinutes maps the daily total; Steps mirrors heart-rate intraday with one
// sample per dataset point. Fixtures mirror the documented schemas.
public sealed class Vo2MaxAzmStepsMapperTests
{
    private static readonly DateTimeOffset Midnight = new(2024, 5, 10, 0, 0, 0, TimeSpan.Zero);

    [Fact]
    public void FitbitProvider_MapsVo2MaxResponse_ToCanonicalSample()
    {
        const string fixture = """
        { "cardioScore": [ { "value": { "vo2Max": "33-37" }, "dateTime": "2024-05-10" } ] }
        """;
        var response = JsonSerializer.Deserialize<FitbitCardioScoreResponse>(fixture)!;

        var samples = Vo2MaxMapper.Map(response);

        samples.Should().ContainSingle();
        var sample = samples[0];
        sample.Type.Should().Be(MetricType.VO2Max);
        sample.Timestamp.Should().Be(Midnight);
        // Average of the "33-37" range.
        sample.Value.Should().Be(35);
        sample.Unit.Should().Be("mlkgmin");
        sample.Resolution.Should().Be(IntradayResolution.Daily);
        sample.Source.Should().Be("fitbit");
    }

    [Fact]
    public void FitbitProvider_MapsVo2MaxResponse_FromSingleNumber()
    {
        const string fixture = """
        { "cardioScore": [ { "value": { "vo2Max": 42.5 }, "dateTime": "2024-05-10" } ] }
        """;
        var response = JsonSerializer.Deserialize<FitbitCardioScoreResponse>(fixture)!;

        Vo2MaxMapper.Map(response)[0].Value.Should().Be(42.5);
    }

    [Fact]
    public void FitbitProvider_MapsActiveZoneMinutesResponse_ToCanonicalSample()
    {
        const string fixture = """
        {
          "activities-active-zone-minutes": [
            {
              "dateTime": "2024-05-10",
              "value": {
                "activeZoneMinutes": 47,
                "fatBurnActiveZoneMinutes": 30,
                "cardioActiveZoneMinutes": 12,
                "peakActiveZoneMinutes": 5
              }
            }
          ]
        }
        """;
        var response = JsonSerializer.Deserialize<FitbitActiveZoneMinutesResponse>(fixture)!;

        var samples = ActiveZoneMinutesMapper.Map(response);

        samples.Should().ContainSingle();
        var sample = samples[0];
        sample.Type.Should().Be(MetricType.ActiveZoneMinutes);
        sample.Timestamp.Should().Be(Midnight);
        sample.Value.Should().Be(47);
        sample.Unit.Should().Be("minutes");
        sample.Resolution.Should().Be(IntradayResolution.Daily);
        sample.Source.Should().Be("fitbit");
    }

    [Fact]
    public void FitbitProvider_MapsStepsResponse_ToCanonicalSamples()
    {
        const string fixture = """
        {
          "activities-steps": [ { "dateTime": "2024-05-10", "value": "8423" } ],
          "activities-steps-intraday": {
            "dataset": [
              { "time": "00:00:00", "value": 0 },
              { "time": "08:30:00", "value": 120 }
            ],
            "datasetInterval": 1,
            "datasetType": "minute"
          }
        }
        """;
        var response = JsonSerializer.Deserialize<FitbitStepsResponse>(fixture)!;

        var samples = StepsMapper.Map(response, IntradayResolution.OneMinute);

        samples.Should().HaveCount(2);
        samples.Select(sample => sample.Timestamp).Should().Equal(
            Midnight,
            new DateTimeOffset(2024, 5, 10, 8, 30, 0, TimeSpan.Zero));
        samples.Select(sample => sample.Value).Should().Equal(0, 120);
        samples.Should().OnlyContain(sample =>
            sample.Type == MetricType.Steps
            && sample.Unit == "steps"
            && sample.Resolution == IntradayResolution.OneMinute
            && sample.Source == "fitbit");
    }
}
