using System.Text.Json;
using FitbitSync.Domain;
using FluentAssertions;

namespace FitbitSync.Providers.GoogleHealth.Tests;

// Extracts per-stage minutes from the SAME "sleep" response we already fetch (stage detail lives in
// sleep.summary.stagesSummary[], indexed by the bare UPPER_SNAKE stage type). One night -> one sample of
// the stage that the requested SleepXxxMinutes metric maps to. Nights without a matching stage entry
// (e.g. a nap with only AWAKE/REM/LIGHT, or a CLASSIC night) are skipped for the absent stage rather than
// emitted as zero. The Google stage literal is DERIVED from the MetricType inside the mapper, so a
// metric can only ever read its own stage (a DEEP<->LIGHT swap is unrepresentable).
public sealed class GoogleSleepStageMapperTests
{
    // A full STAGES night with all four stages present and DISTINCT minutes, so a metric->stage mismatch
    // (e.g. a DEEP/LIGHT swap) reads the wrong number and the per-stage assertions fail.
    private const string FullNightJson = """
        {
          "dataPoints": [
            { "dataSource": { "platform": "FITBIT", "device": { "displayName": "Pixel Watch" } },
              "sleep": {
                "type": "STAGES",
                "interval": { "startTime": "2026-06-28T00:42:00Z", "endTime": "2026-06-28T07:35:00Z" },
                "summary": {
                  "minutesAsleep": "405",
                  "minutesAwake": "20",
                  "stagesSummary": [
                    { "type": "DEEP", "minutes": "90", "count": "4" },
                    { "type": "LIGHT", "minutes": "240", "count": "21" },
                    { "type": "REM", "minutes": "75", "count": "5" },
                    { "type": "AWAKE", "minutes": "20", "count": "7" }
                  ]
                }
              } }
          ],
          "nextPageToken": ""
        }
        """;

    // Mirrors the user's confirmed nap screenshot shape: AWAKE 15, REM 13, LIGHT 55, no DEEP entry.
    private const string NapJson = """
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
    [InlineData(MetricType.SleepDeepMinutes, 90)]
    [InlineData(MetricType.SleepLightMinutes, 240)]
    [InlineData(MetricType.SleepRemMinutes, 75)]
    [InlineData(MetricType.SleepAwakeMinutes, 20)]
    public void Map_DerivesStageFromMetric_ExtractsThatStagesMinutes(MetricType metric, long expectedMinutes)
    {
        var response = JsonSerializer.Deserialize<GoogleSleepResponse>(FullNightJson)!;

        var samples = GoogleSleepStageMapper.Map(response, IntradayResolution.Daily, metric);

        samples.Should().ContainSingle();
        samples[0].Type.Should().Be(metric);
        samples[0].Value.Should().Be(expectedMinutes);
        samples[0].Unit.Should().Be("minutes");
        samples[0].Resolution.Should().Be(IntradayResolution.Daily);
        samples[0].Source.Should().Be("google");
        samples[0].Timestamp.Should().Be(new DateTimeOffset(2026, 6, 28, 0, 42, 0, TimeSpan.Zero));
    }

    [Fact]
    public void Map_SkipsNightMissingTheRequestedStage()
    {
        // No DEEP entry in the nap -> Deep metric yields nothing rather than a spurious zero.
        var response = JsonSerializer.Deserialize<GoogleSleepResponse>(NapJson)!;

        GoogleSleepStageMapper.Map(response, IntradayResolution.Daily, MetricType.SleepDeepMinutes)
            .Should().BeEmpty();
    }

    [Fact]
    public void Map_StageTypeMatchIsCaseInsensitive()
    {
        // The derived literal is upper-case ("LIGHT"); a lower-case data type must still match.
        const string lowerCaseTypeJson = """
            {
              "dataPoints": [
                { "sleep": {
                    "interval": { "startTime": "2026-06-28T00:42:00Z", "endTime": "2026-06-28T07:35:00Z" },
                    "summary": { "stagesSummary": [ { "type": "light", "minutes": "55", "count": "6" } ] }
                  } }
              ]
            }
            """;
        var response = JsonSerializer.Deserialize<GoogleSleepResponse>(lowerCaseTypeJson)!;

        GoogleSleepStageMapper.Map(response, IntradayResolution.Daily, MetricType.SleepLightMinutes)
            .Should().ContainSingle().Which.Value.Should().Be(55);
    }

    [Fact]
    public void Map_SkipsDataPointsMissingSleepOrSummary()
    {
        var response = new GoogleSleepResponse { DataPoints = [new GoogleSleepDataPoint()] };

        GoogleSleepStageMapper.Map(response, IntradayResolution.Daily, MetricType.SleepDeepMinutes)
            .Should().BeEmpty();
    }

    [Fact]
    public void Map_NonSleepStageMetric_Throws()
    {
        var response = JsonSerializer.Deserialize<GoogleSleepResponse>(FullNightJson)!;

        var act = () => GoogleSleepStageMapper.Map(response, IntradayResolution.Daily, MetricType.Steps);

        act.Should().Throw<ArgumentOutOfRangeException>();
    }

    [Fact]
    public void Map_NullResponse_Throws()
    {
        var act = () => GoogleSleepStageMapper.Map(null!, IntradayResolution.Daily, MetricType.SleepDeepMinutes);

        act.Should().Throw<ArgumentNullException>();
    }
}
