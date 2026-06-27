namespace FitbitSync.Application;

public interface ISyncCycleRunner
{
    Task RunCycleAsync(CancellationToken ct = default);
}
