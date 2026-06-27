using Microsoft.Extensions.DependencyInjection;
using NSubstitute;

namespace FitbitSync.Application.Tests;

// Phase 5 (5j/5k): the cycle runner is the unit the scheduler invokes each tick. It opens a fresh DI scope
// (so the scoped engine + its EF stores are per-cycle), drains ALL pending force-sync commands first
// (on-demand requests take priority and share the engine), then runs exactly one scheduled incremental+
// backfill pass. The force-sync queue is a singleton shared across cycles.
public sealed class SyncCycleRunnerTests
{
    private static (ServiceProvider Provider, ISyncEngine Engine) BuildScopedEngine()
    {
        var engine = Substitute.For<ISyncEngine>();
        engine.RunForceSyncAsync(Arg.Any<ForceSyncCommand>(), Arg.Any<CancellationToken>())
            .Returns(call => Task.FromResult(new SyncRunResult(((ForceSyncCommand)call[0]).RunId, 0, 0, 0, SyncRunOutcome.Completed)));
        engine.RunOnceAsync(Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(new SyncRunResult(Guid.NewGuid(), 0, 0, 0, SyncRunOutcome.Completed)));

        var provider = new ServiceCollection()
            .AddScoped(_ => engine)
            .BuildServiceProvider();

        return (provider, engine);
    }

    [Fact]
    public async Task RunCycle_DrainsForceSyncQueue_ThenRunsScheduledOnce()
    {
        var (provider, engine) = BuildScopedEngine();
        await using var _ = provider;

        var queue = new ForceSyncQueue();
        var first = ForceSyncCommand.For([MetricType.SpO2]);
        var second = ForceSyncCommand.ForAll();
        await queue.EnqueueAsync(first);
        await queue.EnqueueAsync(second);

        var runner = new SyncCycleRunner(provider.GetRequiredService<IServiceScopeFactory>(), queue);

        await runner.RunCycleAsync();

        await engine.Received(1).RunForceSyncAsync(Arg.Is<ForceSyncCommand>(c => c.RunId == first.RunId), Arg.Any<CancellationToken>());
        await engine.Received(1).RunForceSyncAsync(Arg.Is<ForceSyncCommand>(c => c.RunId == second.RunId), Arg.Any<CancellationToken>());
        await engine.Received(1).RunOnceAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task RunCycle_WithEmptyQueue_RunsScheduledOnce()
    {
        var (provider, engine) = BuildScopedEngine();
        await using var _ = provider;

        var runner = new SyncCycleRunner(provider.GetRequiredService<IServiceScopeFactory>(), new ForceSyncQueue());

        await runner.RunCycleAsync();

        await engine.DidNotReceive().RunForceSyncAsync(Arg.Any<ForceSyncCommand>(), Arg.Any<CancellationToken>());
        await engine.Received(1).RunOnceAsync(Arg.Any<CancellationToken>());
    }
}
