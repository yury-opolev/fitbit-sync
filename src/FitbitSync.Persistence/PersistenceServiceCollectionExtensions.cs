using FitbitSync.Domain;
using FitbitSync.Security;
using Microsoft.Extensions.DependencyInjection;

namespace FitbitSync.Persistence;

public static class PersistenceServiceCollectionExtensions
{
    public static IServiceCollection AddPersistence(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);

        services.AddScoped(provider => provider.GetRequiredService<EncryptedDbContextFactory>().Create());

        services.AddSingleton<IClock, SystemClock>();
        services.AddSingleton<AuditEntryHasher>();

        services.AddScoped<IMetricRepository, MetricRepository>();
        services.AddScoped<ITokenStore, TokenStore>();
        services.AddScoped<ISyncCheckpointStore, SyncCheckpointStore>();
        services.AddScoped<IAuditTrail, AuditTrail>();
        services.AddScoped<IIntegrityVerifier, IntegrityVerifier>();
        services.AddScoped<ISchemaInitializer, SchemaInitializer>();
        services.AddScoped<KeyRotationService>();

        return services;
    }
}
