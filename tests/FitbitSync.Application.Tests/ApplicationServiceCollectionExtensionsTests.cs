using FitbitSync.Domain;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using NSubstitute;

namespace FitbitSync.Application.Tests;

// Phase 5 (5k): AddSyncEngine wires the whole Application layer into DI. The gate, planner, resilience,
// force-sync queue, cycle runner and scheduler are process-wide singletons (shared budget across scheduled
// and on-demand work); the engine is scoped (it consumes scoped EF stores). The hosted SyncScheduler is
// registered. Domain ports are supplied by the outer layers — faked here so the graph resolves end to end.
public sealed class ApplicationServiceCollectionExtensionsTests
{
    private static ServiceProvider BuildProvider(Action<SyncOptions>? configure = null)
    {
        var services = new ServiceCollection();
        services.AddLogging();

        services.AddSingleton(Substitute.For<IHealthDataProvider>());
        services.AddScoped(_ => Substitute.For<IMetricRepository>());
        services.AddScoped(_ => Substitute.For<ISyncCheckpointStore>());
        services.AddScoped(_ => Substitute.For<IAuditTrail>());
        services.AddSingleton<IClock>(new TestClock(new DateTimeOffset(2024, 5, 10, 0, 0, 0, TimeSpan.Zero)));

        services.AddSyncEngine(configure);

        return services.BuildServiceProvider();
    }

    [Fact]
    public void AddSyncEngine_ResolvesEngine_FromAScope()
    {
        using var provider = BuildProvider();

        using var scope = provider.CreateScope();
        var engine = scope.ServiceProvider.GetRequiredService<ISyncEngine>();

        engine.Should().BeOfType<SyncEngine>();
    }

    [Fact]
    public void AddSyncEngine_RegistersGateAndQueue_AsSharedSingletons()
    {
        using var provider = BuildProvider();

        var gate1 = provider.GetRequiredService<IRateLimitGate>();
        var gate2 = provider.GetRequiredService<IRateLimitGate>();
        var queue1 = provider.GetRequiredService<IForceSyncQueue>();
        var queue2 = provider.GetRequiredService<IForceSyncQueue>();

        gate1.Should().BeSameAs(gate2);
        queue1.Should().BeSameAs(queue2);
    }

    [Fact]
    public void AddSyncEngine_RegistersScheduler_AsHostedService()
    {
        using var provider = BuildProvider();

        var hosted = provider.GetServices<IHostedService>();

        hosted.Should().ContainItemsAssignableTo<SyncScheduler>();
    }

    [Fact]
    public void AddSyncEngine_AppliesOptionOverrides()
    {
        using var provider = BuildProvider(options => options.HourlyRequestBudget = 7);

        provider.GetRequiredService<SyncOptions>().HourlyRequestBudget.Should().Be(7);
    }
}
