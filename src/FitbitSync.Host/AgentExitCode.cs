namespace FitbitSync.Host;

public static class AgentExitCode
{
    public const int Success = 0;

    public const int UsageOrConfigFailure = 1;

    public const int OperationFailed = 2;

    public const int RateLimited = 3;
}
