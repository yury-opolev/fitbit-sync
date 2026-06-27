using FitbitSync.Domain;

namespace FitbitSync.Providers.Fitbit;

// Drives the PKCE authorization-code login flow as an offline, testable service. Begin() produces the
// consent URL plus the verifier/state the caller must hold; CompleteAsync() validates the returned
// state (anti-CSRF), exchanges the code for tokens, persists them, and records an AuthGrant audit entry.
// The actual loopback HTTP listener that drives this lives in the Phase 6 API host.
// The class is public (the Phase 6 host resolves it) but the constructor is internal because it depends
// on internal PKCE seams (decision 5 keeps the CSPRNG seam internal). DI constructs it via a factory in
// AddFitbitProvider; tests construct it directly through InternalsVisibleTo.
public sealed class FitbitAuthorizationService : IAuthorizationService
{
    private readonly PkceGenerator pkceGenerator;
    private readonly AuthorizeUrlBuilder authorizeUrlBuilder;
    private readonly IRandomBytesGenerator randomBytes;
    private readonly FitbitTokenClient tokenClient;
    private readonly ITokenStore store;
    private readonly IAuditTrail audit;
    private readonly FitbitOAuthOptions options;

    internal FitbitAuthorizationService(
        PkceGenerator pkceGenerator,
        AuthorizeUrlBuilder authorizeUrlBuilder,
        IRandomBytesGenerator randomBytes,
        FitbitTokenClient tokenClient,
        ITokenStore store,
        IAuditTrail audit,
        FitbitOAuthOptions options)
    {
        ArgumentNullException.ThrowIfNull(pkceGenerator);
        ArgumentNullException.ThrowIfNull(authorizeUrlBuilder);
        ArgumentNullException.ThrowIfNull(randomBytes);
        ArgumentNullException.ThrowIfNull(tokenClient);
        ArgumentNullException.ThrowIfNull(store);
        ArgumentNullException.ThrowIfNull(audit);
        ArgumentNullException.ThrowIfNull(options);

        this.pkceGenerator = pkceGenerator;
        this.authorizeUrlBuilder = authorizeUrlBuilder;
        this.randomBytes = randomBytes;
        this.tokenClient = tokenClient;
        this.store = store;
        this.audit = audit;
        this.options = options;
    }

    // Start a login: generate PKCE codes + an opaque anti-CSRF state, then build the consent URL.
    public AuthorizationSession Begin()
    {
        var codes = this.pkceGenerator.Generate();
        var state = this.GenerateState();
        var authorizeUrl = this.authorizeUrlBuilder.Build(codes.Challenge, state);

        return new AuthorizationSession(authorizeUrl, state, codes.Verifier);
    }

    // Complete a login: validate the returned state against the one we issued (anti-CSRF), exchange the
    // code for tokens, then persist-before-audit exactly like the refresh paths.
    public async Task<OAuthToken> CompleteAsync(
        AuthorizationSession session,
        string returnedState,
        string code,
        CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(session);
        ArgumentException.ThrowIfNullOrEmpty(returnedState);
        ArgumentException.ThrowIfNullOrEmpty(code);

        if (!string.Equals(returnedState, session.State, StringComparison.Ordinal))
        {
            throw new FitbitAuthenticationException("OAuth state mismatch; authorization rejected (possible CSRF).");
        }

        if (this.options.RedirectUri is null)
        {
            throw new InvalidOperationException("FitbitOAuthOptions.RedirectUri must be set to complete authorization.");
        }

        var token = await this.tokenClient
            .ExchangeCodeAsync(code, session.CodeVerifier, this.options.RedirectUri.ToString(), ct)
            .ConfigureAwait(false);

        await this.store.SaveAsync(token, ct).ConfigureAwait(false);
        await this.audit.AppendAsync("AuthGrant", ct).ConfigureAwait(false);

        return token;
    }

    private string GenerateState()
    {
        Span<byte> seed = stackalloc byte[32];
        this.randomBytes.Fill(seed);
        return Base64UrlEncoder.Encode(seed);
    }
}
