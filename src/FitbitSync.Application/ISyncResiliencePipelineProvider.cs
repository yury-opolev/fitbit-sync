using Polly;

namespace FitbitSync.Application;

public interface ISyncResiliencePipelineProvider
{
    ResiliencePipeline Pipeline { get; }
}
