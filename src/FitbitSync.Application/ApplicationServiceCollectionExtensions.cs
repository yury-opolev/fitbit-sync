using FitbitSync.Domain;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace FitbitSync.Application;

public static class ApplicationServiceCollectionExtensions
{
    public static IServiceCollection AddSyncEngine(this IServiceCollection services, Action<SyncOptions>? configure = null)
    {
        ArgumentNullException.ThrowIfNull(services);

        var options = new SyncOptions();
        configure?.Invoke(options);
        services.TryAddSingleton(options);

        services.TryAddSingleton(TimeProvider.System);
        services.TryAddSingleton<IRateLimitGate, TokenBucketRateLimitGate>();
        services.TryAddSingleton<ISyncPlanner, SyncPlanner>();
        services.TryAddSingleton<ISyncResiliencePipelineProvider, SyncResiliencePipelineProvider>();
        services.TryAddSingleton<IForceSyncQueue, ForceSyncQueue>();
        services.TryAddSingleton<ISyncCycleRunner, SyncCycleRunner>();

        services.TryAddScoped<ISyncEngine, SyncEngine>();

        services.AddHostedService<SyncScheduler>();

        return services;
    }
}
