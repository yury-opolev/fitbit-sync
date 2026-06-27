using Microsoft.Extensions.DependencyInjection;

namespace FitbitSync.Application;

public sealed class SyncCycleRunner : ISyncCycleRunner
{
    private readonly IServiceScopeFactory scopeFactory;
    private readonly IForceSyncQueue forceSyncQueue;

    public SyncCycleRunner(IServiceScopeFactory scopeFactory, IForceSyncQueue forceSyncQueue)
    {
        ArgumentNullException.ThrowIfNull(scopeFactory);
        ArgumentNullException.ThrowIfNull(forceSyncQueue);

        this.scopeFactory = scopeFactory;
        this.forceSyncQueue = forceSyncQueue;
    }

    public async Task RunCycleAsync(CancellationToken ct = default)
    {
        await using var scope = this.scopeFactory.CreateAsyncScope();
        var engine = scope.ServiceProvider.GetRequiredService<ISyncEngine>();

        while (!ct.IsCancellationRequested && this.forceSyncQueue.TryDequeue(out var command) && command is not null)
        {
            await engine.RunForceSyncAsync(command, ct).ConfigureAwait(false);
        }

        if (ct.IsCancellationRequested)
        {
            return;
        }

        await engine.RunOnceAsync(ct).ConfigureAwait(false);
    }
}
