namespace FitbitSync.Application;

public sealed record SyncRunResult(
    Guid RunId,
    int ItemsPlanned,
    int ItemsCompleted,
    int SamplesWritten,
    SyncRunOutcome Outcome);
