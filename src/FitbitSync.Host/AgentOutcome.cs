using FitbitSync.Application;

namespace FitbitSync.Host;

public static class AgentOutcome
{
    public static int ToExitCode(SyncRunOutcome outcome) => outcome switch
    {
        SyncRunOutcome.Completed => AgentExitCode.Success,
        SyncRunOutcome.RateLimited => AgentExitCode.RateLimited,
        _ => AgentExitCode.OperationFailed,
    };

    public static string ToToken(SyncRunOutcome outcome) => outcome switch
    {
        SyncRunOutcome.Completed => "completed",
        SyncRunOutcome.RateLimited => "rateLimited",
        SyncRunOutcome.Cancelled => "cancelled",
        _ => "faulted",
    };
}
