using System.Text;

namespace FitbitSync.Providers.Fitbit;

// Builds the Fitbit authorization (consent) URL for the PKCE authorization-code flow.
internal sealed class AuthorizeUrlBuilder
{
    private readonly FitbitOAuthOptions options;

    public AuthorizeUrlBuilder(FitbitOAuthOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);
        this.options = options;
    }

    // codeChallenge = S256 challenge from PkceGenerator; state = opaque anti-CSRF value (generated/validated by the caller).
    public Uri Build(string codeChallenge, string state)
    {
        ArgumentException.ThrowIfNullOrEmpty(codeChallenge);
        ArgumentException.ThrowIfNullOrEmpty(state);

        if (this.options.RedirectUri is null)
        {
            throw new InvalidOperationException("FitbitOAuthOptions.RedirectUri must be set to build an authorize URL.");
        }

        var query = new StringBuilder();
        Append(query, "response_type", "code");
        Append(query, "client_id", this.options.ClientId);
        Append(query, "redirect_uri", this.options.RedirectUri.ToString());
        Append(query, "scope", string.Join(' ', this.options.Scopes));
        Append(query, "code_challenge", codeChallenge);
        Append(query, "code_challenge_method", "S256");
        Append(query, "state", state);

        var builder = new UriBuilder(this.options.AuthorizationEndpoint) { Query = query.ToString() };
        return builder.Uri;
    }

    private static void Append(StringBuilder query, string key, string value)
    {
        if (query.Length > 0)
        {
            query.Append('&');
        }

        query.Append(Uri.EscapeDataString(key));
        query.Append('=');
        query.Append(Uri.EscapeDataString(value));
    }
}
