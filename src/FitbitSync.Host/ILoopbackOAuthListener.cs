namespace FitbitSync.Host;

// Waits for the Fitbit OAuth provider to redirect the system browser back to the loopback address,
// returning the parsed callback. Implemented by a thin HttpListener shell; abstracted so the login
// orchestration can be reasoned about against a port.
public interface ILoopbackOAuthListener
{
    Task<OAuthCallbackResult> WaitForCallbackAsync(Uri redirectUri, CancellationToken ct = default);
}
