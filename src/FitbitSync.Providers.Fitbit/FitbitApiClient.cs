using System.Net;
using System.Net.Http.Json;
using FitbitSync.Domain;

namespace FitbitSync.Providers.Fitbit;

public sealed class FitbitApiClient
{
    public static readonly Uri BaseAddress = new("https://api.fitbit.com");

    private readonly HttpClient httpClient;
    private readonly RateLimitSnapshotHolder rateLimitHolder;

    public FitbitApiClient(HttpClient httpClient, RateLimitSnapshotHolder rateLimitHolder)
    {
        ArgumentNullException.ThrowIfNull(httpClient);
        ArgumentNullException.ThrowIfNull(rateLimitHolder);

        this.httpClient = httpClient;
        this.rateLimitHolder = rateLimitHolder;
    }

    public RateLimitSnapshot? LatestRateLimit => this.rateLimitHolder.Latest;

    public async Task<T> GetJsonAsync<T>(string relativePath, CancellationToken ct = default)
    {
        using var response = await this.httpClient.GetAsync(relativePath, ct).ConfigureAwait(false);

        if (response.StatusCode == HttpStatusCode.TooManyRequests)
        {
            throw new FitbitRateLimitedException(this.rateLimitHolder.Latest);
        }

        if (response.StatusCode == HttpStatusCode.Unauthorized)
        {
            // A 401 reaching here means BearerTokenHandler already force-refreshed and retried once and STILL got 401 — terminal: re-authorization is required.
            throw new FitbitAuthenticationException("Fitbit returned 401 Unauthorized.");
        }

        response.EnsureSuccessStatusCode();

        var payload = await response.Content.ReadFromJsonAsync<T>(ct).ConfigureAwait(false);

        return payload ?? throw new InvalidOperationException($"Fitbit response body for '{relativePath}' was empty.");
    }
}
