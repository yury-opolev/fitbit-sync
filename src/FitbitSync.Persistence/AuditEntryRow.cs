namespace FitbitSync.Persistence;

public sealed class AuditEntryRow
{
    public long Sequence { get; set; }

    public DateTimeOffset Timestamp { get; set; }

    public string Action { get; set; } = string.Empty;

    public string PrevHash { get; set; } = string.Empty;

    public string EntryHash { get; set; } = string.Empty;
}
