namespace FitbitSync.Providers.Fitbit;

// Returned by FitbitAuthorizationService.Begin: the URL to send the user to, plus the verifier and
// state the caller (Phase 6 host) must hold until the redirect comes back to complete the flow.
public sealed record FitbitAuthorizationSession(Uri AuthorizeUrl, string State, string CodeVerifier);
