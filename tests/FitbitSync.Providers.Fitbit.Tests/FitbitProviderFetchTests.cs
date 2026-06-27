using FitbitSync.Domain;
using FitbitSync.Providers.Fitbit;
using FluentAssertions;
using WireMock.RequestBuilders;
using WireMock.ResponseBuilders;
using WireMock.Server;

namespace FitbitSync.Providers.Fitbit.Tests;

// Phase 3 (3f): FetchAsync resolves the catalog URL, calls the typed client, maps the response, and
// returns samples plus the latest RateLimitSnapshot. HTTP 429 surfaces as FitbitRateLimitedException
// (carrying the snapshot); HTTP 401 surfaces as FitbitAuthenticationException. Driven via WireMock.
public sealed class FitbitProviderFetchTests : IDisposable
{
    private readonly WireMockServer server = WireMockServer.Start();

    private static readonly DateOnly Date = new(2024, 5, 10);
    private const string HeartRatePath = "/activities/heart/date/2024-05-10/1d/1min.json";

    private static MetricFetchRequest HeartRateRequest() =>
        new(MetricType.HeartRate, IntradayResolution.OneMinute, DateRange.SingleDay(Date));

    private const string HeartRateBody = """
    {
      "activities-heart": [ { "dateTime": "2024-05-10", "value": { "restingHeartRate": 58 } } ],
      "activities-heart-intraday": {
        "dataset": [
          { "time": "00:00:00", "value": 64 },
          { "time": "00:01:00", "value": 67 }
        ],
        "datasetInterval": 1,
        "datasetType": "minute"
      }
    }
    """;

    [Fact]
    public async Task FitbitProvider_Fetch_ReturnsSamplesAndRateSnapshot()
    {
        this.server
            .Given(Request.Create().WithPath(HeartRatePath).UsingGet())
            .RespondWith(Response.Create()
                .WithStatusCode(200)
                .WithHeader("Content-Type", "application/json")
                .WithHeader("Fitbit-Rate-Limit-Limit", "150")
                .WithHeader("Fitbit-Rate-Limit-Remaining", "100")
                .WithHeader("Fitbit-Rate-Limit-Reset", "1200")
                .WithBody(HeartRateBody));

        var (provider, _) = ProviderTestHarness.Build(this.server.Urls[0]);

        var result = await provider.FetchAsync(HeartRateRequest());

        result.Samples.Should().HaveCount(2);
        result.Samples.Should().OnlyContain(sample => sample.Type == MetricType.HeartRate && sample.Source == "fitbit");
        result.RateLimit.Should().NotBeNull();
        result.RateLimit!.Limit.Should().Be(150);
        result.RateLimit.Remaining.Should().Be(100);
        result.RateLimit.ResetSeconds.Should().Be(1200);
    }

    [Fact]
    public async Task FitbitProvider_Fetch_Surfaces429AsRateLimited()
    {
        this.server
            .Given(Request.Create().WithPath(HeartRatePath).UsingGet())
            .RespondWith(Response.Create()
                .WithStatusCode(429)
                .WithHeader("Fitbit-Rate-Limit-Limit", "150")
                .WithHeader("Fitbit-Rate-Limit-Remaining", "0")
                .WithHeader("Fitbit-Rate-Limit-Reset", "900"));

        var (provider, _) = ProviderTestHarness.Build(this.server.Urls[0]);

        var fetch = async () => await provider.FetchAsync(HeartRateRequest());

        var thrown = await fetch.Should().ThrowAsync<FitbitRateLimitedException>();
        thrown.Which.RateLimit.Should().NotBeNull();
        thrown.Which.RateLimit!.Remaining.Should().Be(0);
        thrown.Which.RateLimit.ResetSeconds.Should().Be(900);
    }

    [Fact]
    public async Task FitbitProvider_Fetch_Surfaces401AsAuth()
    {
        this.server
            .Given(Request.Create().WithPath(HeartRatePath).UsingGet())
            .RespondWith(Response.Create().WithStatusCode(401));

        var (provider, _) = ProviderTestHarness.Build(this.server.Urls[0]);

        var fetch = async () => await provider.FetchAsync(HeartRateRequest());

        await fetch.Should().ThrowAsync<FitbitAuthenticationException>();
    }

    [Fact]
    public async Task FitbitProvider_Fetch_MapsSleepDay()
    {
        const string sleepPath = "/sleep/date/2024-05-10.json";
        const string sleepBody = """
        {
          "sleep": [ { "dateOfSleep": "2024-05-10", "isMainSleep": true, "minutesAsleep": 421 } ],
          "summary": { "totalMinutesAsleep": 421, "totalSleepRecords": 1, "totalTimeInBed": 450 }
        }
        """;

        this.server
            .Given(Request.Create().WithPath(sleepPath).UsingGet())
            .RespondWith(Response.Create()
                .WithStatusCode(200)
                .WithHeader("Content-Type", "application/json")
                .WithBody(sleepBody));

        var (provider, _) = ProviderTestHarness.Build(this.server.Urls[0]);

        var result = await provider.FetchAsync(
            new MetricFetchRequest(MetricType.Sleep, IntradayResolution.Daily, DateRange.SingleDay(Date)));

        result.Samples.Should().ContainSingle();
        result.Samples[0].Type.Should().Be(MetricType.Sleep);
        result.Samples[0].Value.Should().Be(421);
        result.Samples[0].Unit.Should().Be("minutes");
    }

    public void Dispose() => this.server.Dispose();
}
