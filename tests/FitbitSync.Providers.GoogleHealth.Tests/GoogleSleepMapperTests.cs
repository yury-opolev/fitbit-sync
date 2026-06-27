using System.Text.Json;
using FitbitSync.Domain;
using FluentAssertions;

namespace FitbitSync.Providers.GoogleHealth.Tests;

public sealed class GoogleSleepMapperTests
{
    private const string Json = """
        {
          "dataPoints": [
            { "dataSource": { "platform": "FITBIT" },
              "sleep": { "interval": { "startTime": "2026-06-26T23:00:00Z", "endTime": "2026-06-27T07:12:00Z" },
                         "summary": { "minutesAsleep": "432", "minutesAwake": "30" } } }
          ],
          "nextPageToken": ""
        }
        """;

    [Fact]
    public void Map_ProjectsMinutesAsleepAtSessionStart()
    {
        var response = JsonSerializer.Deserialize<GoogleSleepResponse>(Json)!;

        var samples = GoogleSleepMapper.Map(response, IntradayResolution.Daily);

        samples.Should().ContainSingle();
        samples[0].Type.Should().Be(MetricType.Sleep);
        samples[0].Value.Should().Be(432);
        samples[0].Unit.Should().Be("minutes");
        samples[0].Resolution.Should().Be(IntradayResolution.Daily);
        samples[0].Source.Should().Be("google");
    }
}
