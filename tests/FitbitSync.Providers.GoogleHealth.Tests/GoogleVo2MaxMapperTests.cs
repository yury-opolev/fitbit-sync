using System.Text.Json;
using FitbitSync.Domain;
using FluentAssertions;

namespace FitbitSync.Providers.GoogleHealth.Tests;

public sealed class GoogleVo2MaxMapperTests
{
    private const string Json = """
        {
          "dataPoints": [
            { "dataSource": { "platform": "FITBIT" },
              "vo2Max": { "sampleTime": { "physicalTime": "2026-06-27T08:00:00Z" }, "vo2Max": 44.1, "measurementMethod": "FITBIT_RUN" } }
          ],
          "nextPageToken": ""
        }
        """;

    [Fact]
    public void Map_ProjectsVo2MaxSamples()
    {
        var response = JsonSerializer.Deserialize<GoogleVo2MaxResponse>(Json)!;

        var samples = GoogleVo2MaxMapper.Map(response, IntradayResolution.Daily);

        samples.Should().ContainSingle();
        samples[0].Type.Should().Be(MetricType.VO2Max);
        samples[0].Value.Should().Be(44.1);
        samples[0].Unit.Should().Be("mL/kg/min");
        samples[0].Source.Should().Be("google");
    }
}
