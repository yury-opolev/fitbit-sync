using Polly;
using Polly.Retry;

namespace FitbitSync.Application;

public sealed class SyncResiliencePipelineProvider : ISyncResiliencePipelineProvider
{
    public SyncResiliencePipelineProvider(SyncOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);
        this.Pipeline = Build(options);
    }

    public ResiliencePipeline Pipeline { get; }

    private static ResiliencePipeline Build(SyncOptions options) =>
        new ResiliencePipelineBuilder()
            .AddRetry(new RetryStrategyOptions
            {
                ShouldHandle = new PredicateBuilder()
                    .Handle<HttpRequestException>()
                    .Handle<TimeoutException>()
                    .Handle<IOException>(),
                MaxRetryAttempts = options.MaxRetryAttempts,
                BackoffType = DelayBackoffType.Exponential,
                UseJitter = true,
                Delay = options.RetryBaseDelay,
            })
            .Build();
}
