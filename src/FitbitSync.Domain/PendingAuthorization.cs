namespace FitbitSync.Domain;

// The single in-flight PKCE login, persisted between `login --begin` and `login --complete` so an
// agent can hand the authorize URL to a human and complete the token exchange in a later command.
// Carries the anti-CSRF state, the PKCE code verifier, the authorize URL, and an absolute expiry.
public sealed record PendingAuthorization(
    string State,
    string CodeVerifier,
    Uri AuthorizeUrl,
    DateTimeOffset ExpiresAt)
{
    public bool IsExpired(DateTimeOffset now) => now >= this.ExpiresAt;
}
