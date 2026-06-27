using FitbitSync.Domain;

namespace FitbitSync.Application.Tests;

// Phase 5 (5i): the force-sync queue decouples an on-demand sync request (later raised by the API's
// POST /sync) from the engine. It is an unbounded in-process Channel: enqueue returns immediately with
// the command's run id (for status polling), and the engine drains it FIFO. The command shares the engine,
// gate and checkpoints, so a manual trigger cannot violate rate limits or corrupt cursors.
public sealed class ForceSyncQueueTests
{
    [Fact]
    public async Task Enqueue_ThenDequeue_PreservesFifoOrder()
    {
        var queue = new ForceSyncQueue();
        var first = ForceSyncCommand.For([MetricType.SpO2]);
        var second = ForceSyncCommand.For([MetricType.HeartRate]);

        await queue.EnqueueAsync(first);
        await queue.EnqueueAsync(second);

        queue.TryDequeue(out var dequeuedFirst).Should().BeTrue();
        queue.TryDequeue(out var dequeuedSecond).Should().BeTrue();
        queue.TryDequeue(out _).Should().BeFalse();

        dequeuedFirst!.RunId.Should().Be(first.RunId);
        dequeuedSecond!.RunId.Should().Be(second.RunId);
    }

    [Fact]
    public async Task DequeueAllAsync_YieldsEnqueuedCommands()
    {
        var queue = new ForceSyncQueue();
        await queue.EnqueueAsync(ForceSyncCommand.ForAll());
        await queue.EnqueueAsync(ForceSyncCommand.ForAll());

        var drained = new List<ForceSyncCommand>();

        await foreach (var command in queue.DequeueAllAsync(CancellationToken.None))
        {
            drained.Add(command);
            if (drained.Count == 2)
            {
                break;
            }
        }

        drained.Should().HaveCount(2);
    }

    [Fact]
    public void ForAll_And_For_AssignDistinctRunIds()
    {
        ForceSyncCommand.ForAll().RunId.Should().NotBe(ForceSyncCommand.ForAll().RunId);
        ForceSyncCommand.For([MetricType.Sleep]).Metrics.Should().ContainSingle()
            .Which.Should().Be(MetricType.Sleep);
    }
}
