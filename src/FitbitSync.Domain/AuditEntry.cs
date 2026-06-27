namespace FitbitSync.Domain;

public sealed record AuditEntry(
    long Sequence,
    DateTimeOffset Timestamp,
    string Action,
    string PrevHash,
    string EntryHash);
