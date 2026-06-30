using FitbitSync.Domain;
using FluentAssertions;
using WireMock.RequestBuilders;
using WireMock.ResponseBuilders;
using WireMock.Server;

namespace FitbitSync.Providers.GoogleHealth.Tests;

// End-to-end provider path against a stubbed Google Health endpoint: the request carries a Bearer token and
// a civil-date filter, hits the kebab-case dataType path, and the typed payload is mapped to MetricSamples.
public sealed class GoogleHealthDataProviderTests : IDisposable
{
    private const string StepsPath = "/v4/users/me/dataTypes/steps/dataPoints";

    private const string SleepPath = "/v4/users/me/dataTypes/sleep/dataPoints";

    private const string StepsJson = """
        {
          "dataPoints": [
            { "dataSource": { "platform": "FITBIT", "device": { "displayName": "Charge 6" } },
              "steps": { "interval": { "startTime": "2026-06-27T12:08:00Z", "endTime": "2026-06-27T12:09:00Z" }, "count": "28" } }
          ],
          "nextPageToken": ""
        }
        """;

    // A full STAGES night with all four stages present and DISTINCT minutes, so a provider arm that
    // dispatches the wrong sleep-stage metric reads the wrong number and the assertion fails.
    private const string SleepStagesJson = """
        {
          "dataPoints": [
            { "dataSource": { "platform": "FITBIT", "device": { "displayName": "Pixel Watch" } },
              "sleep": {
                "type": "STAGES",
                "interval": { "startTime": "2026-06-27T00:42:00Z", "endTime": "2026-06-27T07:35:00Z" },
                "summary": {
                  "minutesAsleep": "405",
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

    private readonly WireMockServer server = WireMockServer.Start();

    [Fact]
    public async Task FetchAsync_Steps_CallsGoogleHealthWithBearerAndFilter_AndMapsSamples()
    {
        this.server
            .Given(Request.Create().WithPath(StepsPath).UsingGet())
            .RespondWith(Response.Create().WithStatusCode(200).WithHeader("Content-Type", "application/json").WithBody(StepsJson));
        var provider = this.BuildProvider();

        var request = new MetricFetchRequest(MetricType.Steps, IntradayResolution.OneMinute, DateRange.SingleDay(new DateOnly(2026, 6, 27)));
        var result = await provider.FetchAsync(request, CancellationToken.None);

        result.Samples.Should().ContainSingle();
        result.Samples[0].Type.Should().Be(MetricType.Steps);
        result.Samples[0].Value.Should().Be(28);
        result.Samples[0].Source.Should().Be("google");

        var requestMessage = this.server.LogEntries.Single().RequestMessage;
        requestMessage.Headers!["Authorization"].Single().Should().StartWith("Bearer ");
        requestMessage.Query!["filter"].Single().Should().Contain("steps.interval.civil_start_time");
    }

    [Theory]
    [InlineData(MetricType.SleepDeepMinutes, 90)]
    [InlineData(MetricType.SleepLightMinutes, 240)]
    [InlineData(MetricType.SleepRemMinutes, 75)]
    [InlineData(MetricType.SleepAwakeMinutes, 20)]
    public async Task FetchAsync_SleepStage_DispatchesToCorrectStageMinutes(MetricType metric, long expectedMinutes)
    {
        this.server
            .Given(Request.Create().WithPath(SleepPath).UsingGet())
            .RespondWith(Response.Create().WithStatusCode(200).WithHeader("Content-Type", "application/json").WithBody(SleepStagesJson));
        var provider = this.BuildProvider();

        var request = new MetricFetchRequest(metric, IntradayResolution.Daily, DateRange.SingleDay(new DateOnly(2026, 6, 27)));
        var result = await provider.FetchAsync(request, CancellationToken.None);

        result.Samples.Should().ContainSingle();
        result.Samples[0].Type.Should().Be(metric);
        result.Samples[0].Value.Should().Be(expectedMinutes);
        result.Samples[0].Unit.Should().Be("minutes");
        result.Samples[0].Source.Should().Be("google");

        var requestMessage = this.server.LogEntries.Single().RequestMessage;
        requestMessage.Query!["filter"].Single().Should().Contain("sleep.interval.civil_end_time");
    }

    private GoogleHealthDataProvider BuildProvider()
    {
        var store = new FakeTokenStore(new OAuthToken("access-tok", "refresh-tok", DateTimeOffset.MaxValue, ["scope"]));
        var options = new GoogleOAuthOptions
        {
            ClientId = "c",
            ClientSecret = "s",
            RedirectUri = new Uri("https://localhost:7654/callback"),
            TokenEndpoint = new Uri(this.server.Urls[0] + "/token"),
        };
        var accessTokenSource = new GoogleAccessTokenSource(store, new GoogleTokenClient(new HttpClient(), options, new FixedClock()), new FixedClock(), options);
        var httpClient = new HttpClient { BaseAddress = new Uri(this.server.Urls[0]) };
        return new GoogleHealthDataProvider(new GoogleHealthApiClient(httpClient, accessTokenSource));
    }

    public void Dispose() => this.server.Dispose();

    private sealed class FixedClock : IClock
    {
        public DateTimeOffset UtcNow { get; } = new(2026, 6, 27, 12, 0, 0, TimeSpan.Zero);
    }

    private sealed class FakeTokenStore : ITokenStore
    {
        private OAuthToken? current;

        public FakeTokenStore(OAuthToken? seed) => this.current = seed;

        public Task<OAuthToken?> LoadAsync(CancellationToken ct = default) => Task.FromResult(this.current);

        public Task SaveAsync(OAuthToken token, CancellationToken ct = default)
        {
            this.current = token;
            return Task.CompletedTask;
        }
    }
}
