namespace FitbitSync.Host;

// The outcome of a headless `login --complete` attempt. Authorized is success; every other value is an
// operational failure with a stable error code in the JSON envelope.
public enum LoginCompletionStatus
{
    Authorized,
    NoPendingAuthorization,
    AuthorizationExpired,
    InvalidRedirect,
    AuthorizationDenied,
    StateMismatch,
    TokenExchangeFailed,
}
