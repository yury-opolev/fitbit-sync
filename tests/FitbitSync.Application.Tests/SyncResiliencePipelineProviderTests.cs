using FitbitSync.Domain;
using Polly;

namespace FitbitSync.Application.Tests;

// Phase 5 (5f): provider calls are wrapped in a Polly v8 resilience pipeline that retries only TRANSIENT
// faults (network/timeout/IO) with bounded exponential backoff + jitter. Rate-limit (429) and auth (401)
// are deliberately NOT retried here: 429 is handled by the gate's pause-until-reset, and auth by the token
// layer — retrying either would waste budget. Tests run with zero base delay so nothing waits.
public sealed class SyncResiliencePipelineProviderTests
{
    private static ResiliencePipeline Build(int attempts = 3) =>
        new SyncResiliencePipelineProvider(new SyncOptions
        {
            MaxRetryAttempts = attempts,
            RetryBaseDelay = TimeSpan.Zero,
        }).Pipeline;

    [Fact]
    public async Task Pipeline_RetriesTransientFault_ThenSucceeds()
    {
        var pipeline = Build(attempts: 3);
        var calls = 0;

        var result = await pipeline.ExecuteAsync(async _ =>
        {
            calls++;
            await Task.Yield();
            if (calls < 3)
            {
                throw new HttpRequestException("transient");
            }

            return 42;
        });

        result.Should().Be(42);
        // Two failures then success on the third attempt.
        calls.Should().Be(3);
    }

    [Fact]
    public async Task Pipeline_DoesNotRetry_RateLimited()
    {
        var pipeline = Build(attempts: 3);
        var calls = 0;

        var act = async () => await pipeline.ExecuteAsync<int>(_ =>
        {
            calls++;
            throw new ProviderRateLimitedException(new RateLimitSnapshot(0, 150, 900, DateTimeOffset.UnixEpoch));
        });

        await act.Should().ThrowAsync<ProviderRateLimitedException>();
        // Surfaced on the first throw — no retries burned on a 429.
        calls.Should().Be(1);
    }

    [Fact]
    public async Task Pipeline_DoesNotRetry_Authentication()
    {
        var pipeline = Build(attempts: 3);
        var calls = 0;

        var act = async () => await pipeline.ExecuteAsync<int>(_ =>
        {
            calls++;
            throw new ProviderAuthenticationException("re-auth required");
        });

        await act.Should().ThrowAsync<ProviderAuthenticationException>();
        calls.Should().Be(1);
    }

    [Fact]
    public async Task Pipeline_ExhaustsAttempts_ThenRethrows()
    {
        var pipeline = Build(attempts: 2);
        var calls = 0;

        var act = async () => await pipeline.ExecuteAsync<int>(_ =>
        {
            calls++;
            throw new HttpRequestException("always down");
        });

        await act.Should().ThrowAsync<HttpRequestException>();
        // Initial attempt + 2 retries.
        calls.Should().Be(3);
    }
}
