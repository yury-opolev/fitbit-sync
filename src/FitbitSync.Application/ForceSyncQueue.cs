using System.Threading.Channels;

namespace FitbitSync.Application;

public sealed class ForceSyncQueue : IForceSyncQueue
{
    private readonly Channel<ForceSyncCommand> channel =
        Channel.CreateUnbounded<ForceSyncCommand>(new UnboundedChannelOptions { SingleReader = true });

    public ValueTask EnqueueAsync(ForceSyncCommand command, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(command);
        return this.channel.Writer.WriteAsync(command, ct);
    }

    public IAsyncEnumerable<ForceSyncCommand> DequeueAllAsync(CancellationToken ct = default) =>
        this.channel.Reader.ReadAllAsync(ct);

    public bool TryDequeue(out ForceSyncCommand? command)
    {
        if (this.channel.Reader.TryRead(out var item))
        {
            command = item;
            return true;
        }

        command = null;
        return false;
    }
}
