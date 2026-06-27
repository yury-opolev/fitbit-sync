namespace FitbitSync.Host;

public sealed record AgentResponse(
    int SchemaVersion,
    string Command,
    bool Ok,
    int ExitCode,
    object? Data,
    AgentError? Error)
{
    public const int CurrentSchemaVersion = 1;

    public static AgentResponse Success(string command, int exitCode, object? data) =>
        new(CurrentSchemaVersion, command, true, exitCode, data, null);

    public static AgentResponse ForResult(string command, int exitCode, object? data) =>
        new(CurrentSchemaVersion, command, exitCode == AgentExitCode.Success, exitCode, data, null);

    public static AgentResponse Failure(string command, int exitCode, string errorCode, string message) =>
        new(CurrentSchemaVersion, command, false, exitCode, null, new AgentError(errorCode, message));
}
