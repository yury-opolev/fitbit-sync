using System.Text.Json;
using FitbitSync.Domain;
using FluentAssertions;

namespace FitbitSync.Providers.GoogleHealth.Tests;

public sealed class GoogleActiveZoneMinutesMapperTests
{
    private const string Json = """
        {
          "dataPoints": [
            { "dataSource": { "platform": "FITBIT" },
              "activeZoneMinutes": { "interval": { "startTime": "2026-06-27T08:00:00Z", "endTime": "2026-06-27T08:01:00Z" }, "activeZoneMinutes": "2" } }
          ],
          "nextPageToken": ""
        }
        """;

    [Fact]
    public void Map_ProjectsAzmAtIntervalStart()
    {
        var response = JsonSerializer.Deserialize<GoogleActiveZoneMinutesResponse>(Json)!;

        var samples = GoogleActiveZoneMinutesMapper.Map(response, IntradayResolution.OneMinute);

        samples.Should().ContainSingle();
        samples[0].Type.Should().Be(MetricType.ActiveZoneMinutes);
        samples[0].Value.Should().Be(2);
        samples[0].Unit.Should().Be("minutes");
        samples[0].Timestamp.Should().Be(new DateTimeOffset(2026, 6, 27, 8, 0, 0, TimeSpan.Zero));
    }
}
