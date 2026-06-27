namespace FitbitSync.Domain;

// The in-flight authorization a login starts: the consent URL to send the user to, plus the anti-CSRF
// state and PKCE code verifier the caller holds until the redirect comes back to complete the flow.
// Provider-neutral so the Fitbit and Google authorization services share the headless login machinery.
public sealed record AuthorizationSession(Uri AuthorizeUrl, string State, string CodeVerifier);
