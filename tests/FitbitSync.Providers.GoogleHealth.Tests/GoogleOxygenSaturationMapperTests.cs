using System.Text.Json;
using FitbitSync.Domain;
using FluentAssertions;

namespace FitbitSync.Providers.GoogleHealth.Tests;

public sealed class GoogleOxygenSaturationMapperTests
{
    private const string Json = """
        {
          "dataPoints": [
            { "dataSource": { "platform": "FITBIT" },
              "oxygenSaturation": { "sampleTime": { "physicalTime": "2026-06-27T03:08:00Z" }, "percentage": 97.5 } }
          ],
          "nextPageToken": ""
        }
        """;

    [Fact]
    public void Map_ProjectsSpO2PercentageSamples()
    {
        var response = JsonSerializer.Deserialize<GoogleOxygenSaturationResponse>(Json)!;

        var samples = GoogleOxygenSaturationMapper.Map(response, IntradayResolution.OneMinute);

        samples.Should().ContainSingle();
        samples[0].Type.Should().Be(MetricType.SpO2);
        samples[0].Value.Should().Be(97.5);
        samples[0].Unit.Should().Be("%");
        samples[0].Source.Should().Be("google");
    }
}
