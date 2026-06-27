using System.Text.Json;
using FitbitSync.Domain;
using FluentAssertions;

namespace FitbitSync.Providers.GoogleHealth.Tests;

public sealed class GoogleHeartRateMapperTests
{
    private const string Json = """
        {
          "dataPoints": [
            { "dataSource": { "platform": "FITBIT", "device": { "displayName": "Charge 6" } },
              "heartRate": { "sampleTime": { "physicalTime": "2026-06-27T12:08:00Z" }, "beatsPerMinute": "72" } }
          ],
          "nextPageToken": ""
        }
        """;

    [Fact]
    public void Map_ProjectsBpmSamplesAtSampleTime()
    {
        var response = JsonSerializer.Deserialize<GoogleHeartRateResponse>(Json)!;

        var samples = GoogleHeartRateMapper.Map(response, IntradayResolution.OneMinute);

        samples.Should().ContainSingle();
        samples[0].Type.Should().Be(MetricType.HeartRate);
        samples[0].Value.Should().Be(72);
        samples[0].Unit.Should().Be("bpm");
        samples[0].Timestamp.Should().Be(new DateTimeOffset(2026, 6, 27, 12, 8, 0, TimeSpan.Zero));
        samples[0].Source.Should().Be("google");
    }
}
