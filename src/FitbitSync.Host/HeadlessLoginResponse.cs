namespace FitbitSync.Host;

// Maps a headless `login --complete` outcome to the agent JSON envelope + exit code. Success exposes only
// the `authorized` flag — never tokens, the verifier, or any secret. Every operational failure is exit 2
// (OperationFailed) with a stable error code the agent can branch on.
public static class HeadlessLoginResponse
{
    public const string BeginCommand = "login-begin";

    public const string CompleteCommand = "login-complete";

    public static AgentResponse ForCompletion(LoginCompletionStatus status, string? detail = null) => status switch
    {
        LoginCompletionStatus.Authorized =>
            AgentResponse.Success(CompleteCommand, AgentExitCode.Success, new { authorized = true }),
        LoginCompletionStatus.NoPendingAuthorization =>
            Failure("no_pending_authorization", detail ?? "No pending authorization. Run `login --begin` first."),
        LoginCompletionStatus.AuthorizationExpired =>
            Failure("authorization_expired", detail ?? "The pending authorization has expired. Run `login --begin` again."),
        LoginCompletionStatus.InvalidRedirect =>
            Failure("invalid_redirect", detail ?? "The supplied --redirect was not a valid absolute callback URL."),
        LoginCompletionStatus.AuthorizationDenied =>
            Failure("authorization_denied", detail ?? "Authorization was denied or the callback carried an error."),
        LoginCompletionStatus.StateMismatch =>
            Failure("state_mismatch", detail ?? "OAuth state did not match the pending login (possible CSRF)."),
        LoginCompletionStatus.TokenExchangeFailed =>
            Failure("token_exchange_failed", detail ?? "Exchanging the authorization code for tokens failed."),
        _ => throw new ArgumentOutOfRangeException(nameof(status), status, "Unknown login completion status."),
    };

    private static AgentResponse Failure(string code, string message) =>
        AgentResponse.Failure(CompleteCommand, AgentExitCode.OperationFailed, code, message);
}
