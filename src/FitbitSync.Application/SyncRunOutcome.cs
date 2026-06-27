namespace FitbitSync.Application;

public enum SyncRunOutcome
{
    Completed,
    RateLimited,
    Cancelled,
    Faulted,
}
