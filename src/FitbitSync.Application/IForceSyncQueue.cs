namespace FitbitSync.Application;

public interface IForceSyncQueue
{
    ValueTask EnqueueAsync(ForceSyncCommand command, CancellationToken ct = default);

    IAsyncEnumerable<ForceSyncCommand> DequeueAllAsync(CancellationToken ct = default);

    bool TryDequeue(out ForceSyncCommand? command);
}
