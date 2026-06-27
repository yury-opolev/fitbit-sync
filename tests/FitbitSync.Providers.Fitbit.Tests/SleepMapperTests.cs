using System.Text.Json;
using FitbitSync.Domain;
using FitbitSync.Providers.Fitbit;
using FluentAssertions;

namespace FitbitSync.Providers.Fitbit.Tests;

// Phase 5 (5a): the deferred Sleep mapper. The Fitbit 1.2 "Get Sleep Log by Date" response lists sleep
// logs (a main sleep plus any naps); we map the MAIN sleep log's minutesAsleep to exactly one canonical
// MetricSample — type Sleep, a midnight-UTC timestamp from dateOfSleep, value = minutesAsleep, unit
// "minutes", Daily resolution, source "fitbit". Naps (isMainSleep=false) are excluded so a day yields a
// single nightly figure. Fixtures mirror the documented schema (extra fields are tolerated/ignored).
public sealed class SleepMapperTests
{
    private static readonly DateTimeOffset Midnight = new(2024, 5, 10, 0, 0, 0, TimeSpan.Zero);

    [Fact]
    public void FitbitProvider_MapsSleepResponse_ToCanonicalSample()
    {
        const string fixture = """
        {
          "sleep": [
            {
              "dateOfSleep": "2024-05-10",
              "isMainSleep": true,
              "minutesAsleep": 432,
              "minutesAwake": 30,
              "timeInBed": 462,
              "efficiency": 92,
              "type": "stages"
            }
          ],
          "summary": { "totalMinutesAsleep": 432, "totalSleepRecords": 1, "totalTimeInBed": 462 }
        }
        """;
        var response = JsonSerializer.Deserialize<FitbitSleepResponse>(fixture)!;

        var samples = SleepMapper.Map(response);

        samples.Should().ContainSingle();
        var sample = samples[0];
        sample.Type.Should().Be(MetricType.Sleep);
        sample.Timestamp.Should().Be(Midnight);
        sample.Value.Should().Be(432);
        sample.Unit.Should().Be("minutes");
        sample.Resolution.Should().Be(IntradayResolution.Daily);
        sample.Source.Should().Be("fitbit");
    }

    [Fact]
    public void FitbitProvider_MapsSleepResponse_ExcludesNaps()
    {
        // A day can include nap logs (isMainSleep=false) alongside the main sleep; only the main
        // sleep represents the nightly figure we persist.
        const string fixture = """
        {
          "sleep": [
            { "dateOfSleep": "2024-05-10", "isMainSleep": false, "minutesAsleep": 25 },
            { "dateOfSleep": "2024-05-10", "isMainSleep": true, "minutesAsleep": 432 }
          ]
        }
        """;
        var response = JsonSerializer.Deserialize<FitbitSleepResponse>(fixture)!;

        var samples = SleepMapper.Map(response);

        samples.Should().ContainSingle();
        samples[0].Value.Should().Be(432);
    }
}
