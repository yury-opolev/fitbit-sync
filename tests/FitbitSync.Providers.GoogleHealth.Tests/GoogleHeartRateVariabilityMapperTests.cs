using System.Text.Json;
using FitbitSync.Domain;
using FluentAssertions;

namespace FitbitSync.Providers.GoogleHealth.Tests;

public sealed class GoogleHeartRateVariabilityMapperTests
{
    private const string Json = """
        {
          "dataPoints": [
            { "dataSource": { "platform": "FITBIT" },
              "heartRateVariability": { "sampleTime": { "physicalTime": "2026-06-27T03:08:00Z" }, "rootMeanSquareOfSuccessiveDifferencesMilliseconds": 42.3 } }
          ],
          "nextPageToken": ""
        }
        """;

    [Fact]
    public void Map_ProjectsRmssdSamples()
    {
        var response = JsonSerializer.Deserialize<GoogleHeartRateVariabilityResponse>(Json)!;

        var samples = GoogleHeartRateVariabilityMapper.Map(response, IntradayResolution.OneMinute);

        samples.Should().ContainSingle();
        samples[0].Type.Should().Be(MetricType.Hrv);
        samples[0].Value.Should().Be(42.3);
        samples[0].Unit.Should().Be("ms");
        samples[0].Source.Should().Be("google");
    }
}
