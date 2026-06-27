using FitbitSync.Domain;

namespace FitbitSync.Application;

public interface ISyncPlanner
{
    IReadOnlyList<SyncWorkItem> PlanScheduledWork(
        IReadOnlyList<MetricCapability> capabilities,
        IReadOnlyDictionary<MetricType, SyncCheckpoint?> checkpoints,
        DateOnly today);
}
