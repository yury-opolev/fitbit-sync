namespace FitbitSync.Domain;

// Provider-neutral OAuth login port. Begin() composes the consent URL plus PKCE/state; CompleteAsync()
// validates the returned state (anti-CSRF), exchanges the authorization code for tokens, persists them,
// and audits. Implemented per provider (Fitbit, Google Health). On exchange failure implementations
// throw a ProviderAuthenticationException.
public interface IAuthorizationService
{
    AuthorizationSession Begin();

    Task<OAuthToken> CompleteAsync(AuthorizationSession session, string returnedState, string code, CancellationToken ct = default);
}
