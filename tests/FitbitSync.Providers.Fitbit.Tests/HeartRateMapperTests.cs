using System.Text.Json;
using FitbitSync.Domain;
using FitbitSync.Providers.Fitbit;
using FluentAssertions;

namespace FitbitSync.Providers.Fitbit.Tests;

// Phase 3 named red test: the Fitbit heart-rate intraday response must map to canonical domain
// MetricSamples — one per intraday dataset point, with the Timestamp composed from the day's date
// (activities-heart[0].dateTime) and each point's time-of-day, unit "bpm", the requested resolution,
// and source "fitbit". The fixture mirrors the documented GET activities/heart intraday schema.
public sealed class HeartRateMapperTests
{
    private const string IntradayFixture = """
    {
      "activities-heart": [
        {
          "dateTime": "2024-05-01",
          "value": { "restingHeartRate": 58 }
        }
      ],
      "activities-heart-intraday": {
        "dataset": [
          { "time": "00:00:00", "value": 64 },
          { "time": "00:01:00", "value": 67 },
          { "time": "08:30:00", "value": 92 }
        ],
        "datasetInterval": 1,
        "datasetType": "minute"
      }
    }
    """;

    [Fact]
    public void FitbitProvider_MapsHeartRateResponse_ToCanonicalSamples()
    {
        var response = JsonSerializer.Deserialize<FitbitHeartRateResponse>(IntradayFixture)!;

        var samples = HeartRateMapper.Map(response, IntradayResolution.OneMinute);

        // One sample per intraday dataset point.
        samples.Should().HaveCount(3);

        // Date (from activities-heart[0].dateTime) composed with each point's time-of-day.
        samples.Select(sample => sample.Timestamp).Should().Equal(
            new DateTimeOffset(2024, 5, 1, 0, 0, 0, TimeSpan.Zero),
            new DateTimeOffset(2024, 5, 1, 0, 1, 0, TimeSpan.Zero),
            new DateTimeOffset(2024, 5, 1, 8, 30, 0, TimeSpan.Zero));

        samples.Select(sample => sample.Value).Should().Equal(64, 67, 92);

        samples.Should().OnlyContain(sample =>
            sample.Type == MetricType.HeartRate
            && sample.Unit == "bpm"
            && sample.Resolution == IntradayResolution.OneMinute
            && sample.Source == "fitbit");
    }
}
