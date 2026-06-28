using System.Text.Json;
using FitbitSync.Domain;
using FluentAssertions;

namespace FitbitSync.Providers.GoogleHealth.Tests;

// Maps the verified live "basal-energy-burned" response shape (interval-typed; kcal as a JSON number) into
// domain MetricSamples timestamped at the interval start. Points missing an interval/kcal are skipped.
public sealed class GoogleBasalEnergyBurnedMapperTests
{
    private const string Json = """
        {
          "dataPoints": [
            { "dataSource": { "platform": "FITBIT", "device": { "displayName": "Charge 6" } },
              "basalEnergyBurned": { "interval": { "startTime": "2026-04-20T00:00:00Z", "endTime": "2026-04-20T01:00:00Z" }, "kcal": 68.0 } }
          ],
          "nextPageToken": ""
        }
        """;

    [Fact]
    public void Map_ProjectsKcalIntervalsToSamples()
    {
        var response = JsonSerializer.Deserialize<GoogleBasalEnergyBurnedResponse>(Json)!;

        var samples = GoogleBasalEnergyBurnedMapper.Map(response, IntradayResolution.OneMinute);

        samples.Should().HaveCount(1);
        samples[0].Type.Should().Be(MetricType.BasalCaloriesBurned);
        samples[0].Value.Should().Be(68.0);
        samples[0].Unit.Should().Be("kcal");
        samples[0].Source.Should().Be("google");
        samples[0].Timestamp.Should().Be(new DateTimeOffset(2026, 4, 20, 0, 0, 0, TimeSpan.Zero));
    }

    [Fact]
    public void Map_SkipsDataPointsMissingIntervalOrKcal()
    {
        var response = new GoogleBasalEnergyBurnedResponse { DataPoints = [new GoogleBasalEnergyBurnedDataPoint()] };

        GoogleBasalEnergyBurnedMapper.Map(response, IntradayResolution.OneMinute).Should().BeEmpty();
    }
}
