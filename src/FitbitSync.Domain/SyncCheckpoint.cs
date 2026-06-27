namespace FitbitSync.Domain;

public sealed record SyncCheckpoint(
    MetricType Metric,
    DateTimeOffset? NewestSynced,
    DateTimeOffset? OldestBackfilled)
{
    public SyncCheckpoint AdvanceForward(DateTimeOffset to)
    {
        var newest = this.NewestSynced is { } current && current >= to ? current : to;
        return this with { NewestSynced = newest };
    }

    public SyncCheckpoint ExtendBackfill(DateTimeOffset to)
    {
        var oldest = this.OldestBackfilled is { } current && current <= to ? current : to;
        return this with { OldestBackfilled = oldest };
    }
}
