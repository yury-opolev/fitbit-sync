using FitbitSync.Domain;

namespace FitbitSync.Application;

public sealed class SyncPlanner : ISyncPlanner
{
    private readonly SyncOptions options;

    public SyncPlanner(SyncOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);
        this.options = options;
    }

    public IReadOnlyList<SyncWorkItem> PlanScheduledWork(
        IReadOnlyList<MetricCapability> capabilities,
        IReadOnlyDictionary<MetricType, SyncCheckpoint?> checkpoints,
        DateOnly today)
    {
        ArgumentNullException.ThrowIfNull(capabilities);
        ArgumentNullException.ThrowIfNull(checkpoints);

        var incremental = new List<SyncWorkItem>();
        var backfill = new List<SyncWorkItem>();

        foreach (var capability in capabilities)
        {
            checkpoints.TryGetValue(capability.Metric, out var checkpoint);
            incremental.AddRange(this.PlanIncremental(capability, checkpoint, today));
            backfill.AddRange(this.PlanBackfill(capability, checkpoint, today));
        }

        return [.. incremental, .. backfill];
    }

    private IEnumerable<SyncWorkItem> PlanIncremental(MetricCapability capability, SyncCheckpoint? checkpoint, DateOnly today)
    {
        if (checkpoint?.NewestSynced is not { } newest)
        {
            yield return new SyncWorkItem(capability.Metric, capability.Resolution, today, SyncWorkKind.Incremental);
            yield break;
        }

        for (var date = DateOnly.FromDateTime(newest.UtcDateTime).AddDays(1); date <= today; date = date.AddDays(1))
        {
            yield return new SyncWorkItem(capability.Metric, capability.Resolution, date, SyncWorkKind.Incremental);
        }
    }

    private IEnumerable<SyncWorkItem> PlanBackfill(MetricCapability capability, SyncCheckpoint? checkpoint, DateOnly today)
    {
        var floor = today.AddDays(-(int)Math.Round(this.options.BackfillWindow.TotalDays));

        var anchor = checkpoint?.OldestBackfilled ?? checkpoint?.NewestSynced;
        var start = anchor is { } known ? DateOnly.FromDateTime(known.UtcDateTime).AddDays(-1) : today.AddDays(-1);

        for (var date = start; date >= floor; date = date.AddDays(-1))
        {
            yield return new SyncWorkItem(capability.Metric, capability.Resolution, date, SyncWorkKind.Backfill);
        }
    }
}
