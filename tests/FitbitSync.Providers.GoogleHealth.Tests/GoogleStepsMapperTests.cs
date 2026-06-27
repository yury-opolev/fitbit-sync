using System.Text.Json;
using FitbitSync.Domain;
using FluentAssertions;

namespace FitbitSync.Providers.GoogleHealth.Tests;

// Maps the verified live "steps" response shape (one data point per recorded interval, count as a string)
// into domain MetricSamples timestamped at the interval start. Points missing an interval/count are skipped.
public sealed class GoogleStepsMapperTests
{
    private const string Json = """
        {
          "dataPoints": [
            { "dataSource": { "platform": "FITBIT", "device": { "displayName": "Charge 6" } },
              "steps": { "interval": { "startTime": "2026-06-27T12:08:00Z", "endTime": "2026-06-27T12:09:00Z" }, "count": "28" } },
            { "dataSource": { "platform": "FITBIT", "device": { "displayName": "Charge 6" } },
              "steps": { "interval": { "startTime": "2026-06-27T12:06:00Z", "endTime": "2026-06-27T12:07:00Z" }, "count": "41" } }
          ],
          "nextPageToken": ""
        }
        """;

    [Fact]
    public void Map_ProjectsStepIntervalsToSamples()
    {
        var response = JsonSerializer.Deserialize<GoogleStepsResponse>(Json)!;

        var samples = GoogleStepsMapper.Map(response, IntradayResolution.OneMinute);

        samples.Should().HaveCount(2);
        samples[0].Type.Should().Be(MetricType.Steps);
        samples[0].Value.Should().Be(28);
        samples[0].Unit.Should().Be("steps");
        samples[0].Resolution.Should().Be(IntradayResolution.OneMinute);
        samples[0].Source.Should().Be("google");
        samples[0].Timestamp.Should().Be(new DateTimeOffset(2026, 6, 27, 12, 8, 0, TimeSpan.Zero));
        samples[1].Value.Should().Be(41);
    }

    [Fact]
    public void Map_SkipsDataPointsMissingIntervalOrCount()
    {
        var response = new GoogleStepsResponse { DataPoints = [new GoogleStepsDataPoint()] };

        GoogleStepsMapper.Map(response, IntradayResolution.OneMinute).Should().BeEmpty();
    }
}
