using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace FitbitSync.Application;

public sealed partial class SyncScheduler : BackgroundService
{
    private readonly ISyncCycleRunner cycleRunner;
    private readonly TimeProvider timeProvider;
    private readonly SyncOptions options;
    private readonly ILogger<SyncScheduler> logger;

    public SyncScheduler(
        ISyncCycleRunner cycleRunner,
        TimeProvider timeProvider,
        SyncOptions options,
        ILogger<SyncScheduler> logger)
    {
        ArgumentNullException.ThrowIfNull(cycleRunner);
        ArgumentNullException.ThrowIfNull(timeProvider);
        ArgumentNullException.ThrowIfNull(options);
        ArgumentNullException.ThrowIfNull(logger);

        this.cycleRunner = cycleRunner;
        this.timeProvider = timeProvider;
        this.options = options;
        this.logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(this.options.Cadence, this.timeProvider);

        do
        {
            await this.RunCycleSafelyAsync(stoppingToken).ConfigureAwait(false);
        }
        while (await WaitForNextTickAsync(timer, stoppingToken).ConfigureAwait(false));
    }

    private async Task RunCycleSafelyAsync(CancellationToken stoppingToken)
    {
        try
        {
            await this.cycleRunner.RunCycleAsync(stoppingToken).ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
        {
        }
        catch (Exception ex)
        {
            this.LogCycleFailed(ex);
        }
    }

    private static async Task<bool> WaitForNextTickAsync(PeriodicTimer timer, CancellationToken stoppingToken)
    {
        try
        {
            return await timer.WaitForNextTickAsync(stoppingToken).ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            return false;
        }
    }

    [LoggerMessage(Level = LogLevel.Error, Message = "Sync cycle failed; continuing with the next scheduled cycle.")]
    private partial void LogCycleFailed(Exception exception);
}
