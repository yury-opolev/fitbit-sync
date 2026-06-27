using Microsoft.Extensions.Logging.Abstractions;

namespace FitbitSync.Application.Tests;

// Phase 5 (5j): the scheduler is a BackgroundService that runs a cycle immediately on start and then once
// per cadence tick (default 15 min) using an injected TimeProvider so tests fire ticks deterministically.
// A failing cycle must be swallowed and logged so the long-running worker never dies — the next tick still
// runs. Driven by ManualTimeProvider (no real waiting).
public sealed class SyncSchedulerTests
{
    private sealed class CountingCycleRunner : ISyncCycleRunner
    {
        private readonly bool throwEachCycle;

        public CountingCycleRunner(bool throwEachCycle = false)
        {
            this.throwEachCycle = throwEachCycle;
        }

        public int Cycles { get; private set; }

        public Task RunCycleAsync(CancellationToken ct = default)
        {
            this.Cycles++;
            if (this.throwEachCycle)
            {
                throw new InvalidOperationException("cycle blew up");
            }

            return Task.CompletedTask;
        }
    }

    private static async Task WaitForAsync(Func<bool> condition)
    {
        for (var i = 0; i < 200 && !condition(); i++)
        {
            await Task.Delay(10);
        }
    }

    [Fact]
    public async Task Scheduler_RunsCycleImmediately_OnStart()
    {
        var runner = new CountingCycleRunner();
        var time = new ManualTimeProvider();
        var scheduler = new SyncScheduler(runner, time, new SyncOptions(), NullLogger<SyncScheduler>.Instance);

        await scheduler.StartAsync(CancellationToken.None);
        await WaitForAsync(() => runner.Cycles >= 1);
        await scheduler.StopAsync(CancellationToken.None);

        runner.Cycles.Should().BeGreaterThanOrEqualTo(1);
    }

    [Fact]
    public async Task Scheduler_RunsAnotherCycle_OnEachTick()
    {
        var runner = new CountingCycleRunner();
        var time = new ManualTimeProvider();
        var scheduler = new SyncScheduler(runner, time, new SyncOptions(), NullLogger<SyncScheduler>.Instance);

        await scheduler.StartAsync(CancellationToken.None);
        await WaitForAsync(() => runner.Cycles >= 1);
        var afterStart = runner.Cycles;

        // Fire a cadence tick: the scheduler wakes and runs another cycle.
        await WaitForAsync(() => time.TimerCount >= 1);
        time.FireAll();
        await WaitForAsync(() => runner.Cycles > afterStart);
        await scheduler.StopAsync(CancellationToken.None);

        runner.Cycles.Should().BeGreaterThan(afterStart);
    }

    [Fact]
    public async Task Scheduler_SwallowsCycleException_AndKeepsRunning()
    {
        var runner = new CountingCycleRunner(throwEachCycle: true);
        var time = new ManualTimeProvider();
        var scheduler = new SyncScheduler(runner, time, new SyncOptions(), NullLogger<SyncScheduler>.Instance);

        await scheduler.StartAsync(CancellationToken.None);
        await WaitForAsync(() => runner.Cycles >= 1);

        // Even though the first cycle threw, the service is alive and ticks still drive more cycles.
        await WaitForAsync(() => time.TimerCount >= 1);
        time.FireAll();
        await WaitForAsync(() => runner.Cycles >= 2);
        await scheduler.StopAsync(CancellationToken.None);

        runner.Cycles.Should().BeGreaterThanOrEqualTo(2);
    }
}
