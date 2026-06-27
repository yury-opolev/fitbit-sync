using System.Web;

namespace FitbitSync.Providers.GoogleHealth;

// Builds the Google OAuth 2.0 authorize URL for the confidential "web" client. Requests offline access +
// forced consent so a refresh token is issued, carries the anti-CSRF state, and the configured scopes.
// Deliberately omits include_granted_scopes — it would merge unrelated granted scopes (e.g. Gmail) into
// the token, which the Health API then rejects (403 DISALLOWED_OAUTH_SCOPES).
public sealed class GoogleAuthorizeUrlBuilder
{
    private readonly GoogleOAuthOptions options;

    public GoogleAuthorizeUrlBuilder(GoogleOAuthOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);
        this.options = options;
    }

    public Uri Build(string state)
    {
        ArgumentException.ThrowIfNullOrEmpty(state);

        if (this.options.RedirectUri is null)
        {
            throw new InvalidOperationException("GoogleOAuthOptions.RedirectUri must be set to build the authorize URL.");
        }

        var query = HttpUtility.ParseQueryString(string.Empty);
        query["response_type"] = "code";
        query["client_id"] = this.options.ClientId;
        query["redirect_uri"] = this.options.RedirectUri.ToString();
        query["scope"] = string.Join(' ', this.options.Scopes);
        query["access_type"] = "offline";
        query["prompt"] = "consent";
        query["state"] = state;

        return new UriBuilder(this.options.AuthorizationEndpoint) { Query = query.ToString() }.Uri;
    }
}
