using System.Text.Json;
using FitbitSync.Domain;
using FluentAssertions;

namespace FitbitSync.Providers.GoogleHealth.Tests;

// Extracts per-stage minutes from the SAME "sleep" response we already fetch (stage detail lives in
// sleep.summary.stagesSummary[], indexed by the bare UPPER_SNAKE stage type). One night -> one sample of
// the requested stage. Nights without a matching stage entry (e.g. a nap with only AWAKE/REM/LIGHT, or a
// CLASSIC night) are skipped for the absent stage rather than emitted as zero.
public sealed class GoogleSleepStageMapperTests
{
    // Mirrors the user's confirmed nap screenshot shape: AWAKE 15, REM 13, LIGHT 55, no DEEP entry.
    private const string Json = """
        {
          "dataPoints": [
            { "dataSource": { "platform": "FITBIT", "device": { "displayName": "Pixel Watch" } },
              "sleep": {
                "type": "STAGES",
                "interval": { "startTime": "2026-06-28T16:13:00Z", "endTime": "2026-06-28T17:21:00Z" },
                "summary": {
                  "minutesAsleep": "68",
                  "minutesAwake": "15",
                  "stagesSummary": [
                    { "type": "AWAKE", "minutes": "15", "count": "3" },
                    { "type": "REM", "minutes": "13", "count": "2" },
                    { "type": "LIGHT", "minutes": "55", "count": "6" }
                  ]
                }
              } }
          ],
          "nextPageToken": ""
        }
        """;

    [Theory]
    [InlineData("LIGHT", MetricType.SleepLightMinutes, 55)]
    [InlineData("REM", MetricType.SleepRemMinutes, 13)]
    [InlineData("AWAKE", MetricType.SleepAwakeMinutes, 15)]
    public void Map_ExtractsRequestedStageMinutes(string stageType, MetricType metric, long expectedMinutes)
    {
        var response = JsonSerializer.Deserialize<GoogleSleepResponse>(Json)!;

        var samples = GoogleSleepStageMapper.Map(response, IntradayResolution.Daily, stageType, metric);

        samples.Should().HaveCount(1);
        samples[0].Type.Should().Be(metric);
        samples[0].Value.Should().Be(expectedMinutes);
        samples[0].Unit.Should().Be("minutes");
        samples[0].Source.Should().Be("google");
        samples[0].Timestamp.Should().Be(new DateTimeOffset(2026, 6, 28, 16, 13, 0, TimeSpan.Zero));
    }

    [Fact]
    public void Map_SkipsNightMissingTheRequestedStage()
    {
        // No DEEP entry in the nap -> Deep mapper yields nothing rather than a spurious zero.
        var response = JsonSerializer.Deserialize<GoogleSleepResponse>(Json)!;

        GoogleSleepStageMapper.Map(response, IntradayResolution.Daily, "DEEP", MetricType.SleepDeepMinutes)
            .Should().BeEmpty();
    }

    [Fact]
    public void Map_StageTypeMatchIsCaseInsensitive()
    {
        var response = JsonSerializer.Deserialize<GoogleSleepResponse>(Json)!;

        GoogleSleepStageMapper.Map(response, IntradayResolution.Daily, "light", MetricType.SleepLightMinutes)
            .Should().ContainSingle().Which.Value.Should().Be(55);
    }

    [Fact]
    public void Map_SkipsDataPointsMissingSleepOrSummary()
    {
        var response = new GoogleSleepResponse { DataPoints = [new GoogleSleepDataPoint()] };

        GoogleSleepStageMapper.Map(response, IntradayResolution.Daily, "DEEP", MetricType.SleepDeepMinutes)
            .Should().BeEmpty();
    }
}
