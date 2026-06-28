using System.Text.Json;
using FitbitSync.Domain;
using FluentAssertions;

namespace FitbitSync.Providers.GoogleHealth.Tests;

// Maps the verified live "active-energy-burned" response shape (interval-typed; kcal as a JSON number) into
// domain MetricSamples timestamped at the interval start. Points missing an interval/kcal are skipped.
public sealed class GoogleActiveEnergyBurnedMapperTests
{
    private const string Json = """
        {
          "dataPoints": [
            { "dataSource": { "platform": "FITBIT", "device": { "displayName": "Charge 6" } },
              "activeEnergyBurned": { "interval": { "startTime": "2026-04-20T08:00:00Z", "endTime": "2026-04-20T08:30:00Z" }, "kcal": 150.0 } },
            { "dataSource": { "platform": "FITBIT", "device": { "displayName": "Charge 6" } },
              "activeEnergyBurned": { "interval": { "startTime": "2026-04-20T08:30:00Z", "endTime": "2026-04-20T09:00:00Z" }, "kcal": 72.5 } }
          ],
          "nextPageToken": ""
        }
        """;

    [Fact]
    public void Map_ProjectsKcalIntervalsToSamples()
    {
        var response = JsonSerializer.Deserialize<GoogleActiveEnergyBurnedResponse>(Json)!;

        var samples = GoogleActiveEnergyBurnedMapper.Map(response, IntradayResolution.OneMinute);

        samples.Should().HaveCount(2);
        samples[0].Type.Should().Be(MetricType.ActiveCaloriesBurned);
        samples[0].Value.Should().Be(150.0);
        samples[0].Unit.Should().Be("kcal");
        samples[0].Resolution.Should().Be(IntradayResolution.OneMinute);
        samples[0].Source.Should().Be("google");
        samples[0].Timestamp.Should().Be(new DateTimeOffset(2026, 4, 20, 8, 0, 0, TimeSpan.Zero));
        samples[1].Value.Should().Be(72.5);
    }

    [Fact]
    public void Map_SkipsDataPointsMissingIntervalOrKcal()
    {
        var response = new GoogleActiveEnergyBurnedResponse { DataPoints = [new GoogleActiveEnergyBurnedDataPoint()] };

        GoogleActiveEnergyBurnedMapper.Map(response, IntradayResolution.OneMinute).Should().BeEmpty();
    }
}
