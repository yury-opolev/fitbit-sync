using System.Security.Cryptography;
using FitbitSync.Domain;

namespace FitbitSync.Providers.GoogleHealth;

// The Google implementation of the provider-neutral IAuthorizationService. Begin() composes the consent
// URL plus an opaque anti-CSRF state. CompleteAsync() validates the returned state BEFORE any network
// call (CSRF guard), exchanges the code for tokens, then persists-before-audit exactly like the Fitbit
// path. The confidential web client authenticates with client_secret, so no PKCE verifier is used.
public sealed class GoogleAuthorizationService : IAuthorizationService
{
    private readonly GoogleAuthorizeUrlBuilder authorizeUrlBuilder;
    private readonly GoogleTokenClient tokenClient;
    private readonly ITokenStore store;
    private readonly IAuditTrail audit;

    public GoogleAuthorizationService(
        GoogleAuthorizeUrlBuilder authorizeUrlBuilder,
        GoogleTokenClient tokenClient,
        ITokenStore store,
        IAuditTrail audit)
    {
        ArgumentNullException.ThrowIfNull(authorizeUrlBuilder);
        ArgumentNullException.ThrowIfNull(tokenClient);
        ArgumentNullException.ThrowIfNull(store);
        ArgumentNullException.ThrowIfNull(audit);

        this.authorizeUrlBuilder = authorizeUrlBuilder;
        this.tokenClient = tokenClient;
        this.store = store;
        this.audit = audit;
    }

    public AuthorizationSession Begin()
    {
        var state = GenerateState();
        var authorizeUrl = this.authorizeUrlBuilder.Build(state);

        return new AuthorizationSession(authorizeUrl, state, string.Empty);
    }

    public async Task<OAuthToken> CompleteAsync(AuthorizationSession session, string returnedState, string code, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(session);
        ArgumentException.ThrowIfNullOrEmpty(returnedState);
        ArgumentException.ThrowIfNullOrEmpty(code);

        if (!string.Equals(returnedState, session.State, StringComparison.Ordinal))
        {
            throw new GoogleAuthenticationException("OAuth state mismatch; authorization rejected (possible CSRF).");
        }

        var token = await this.tokenClient.ExchangeCodeAsync(code, ct).ConfigureAwait(false);

        await this.store.SaveAsync(token, ct).ConfigureAwait(false);
        await this.audit.AppendAsync("AuthGrant", ct).ConfigureAwait(false);

        return token;
    }

    private static string GenerateState() => Convert.ToHexString(RandomNumberGenerator.GetBytes(16));
}
