namespace FitbitSync.Application;

public interface ISyncEngine
{
    Task<SyncRunResult> RunOnceAsync(CancellationToken ct = default);

    Task<SyncRunResult> RunForceSyncAsync(ForceSyncCommand command, CancellationToken ct = default);

    Task<BackfillResult> RunBackfillAsync(BackfillCommand command, CancellationToken ct = default);
}
