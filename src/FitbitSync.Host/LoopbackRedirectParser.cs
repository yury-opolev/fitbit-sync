using System.Web;

namespace FitbitSync.Host;

// Pure translation of a loopback redirect URL's query string into an OAuthCallbackResult.
// Fitbit appends ?code=...&state=... on success or ?error=...&error_description=... on denial.
public static class LoopbackRedirectParser
{
    public static OAuthCallbackResult Parse(Uri redirect)
    {
        ArgumentNullException.ThrowIfNull(redirect);

        var query = HttpUtility.ParseQueryString(redirect.Query);

        var error = NullIfBlank(query["error"]);
        if (error is not null)
        {
            var description = NullIfBlank(query["error_description"]);
            return OAuthCallbackResult.Failure(description is null ? error : $"{error}: {description}");
        }

        var code = NullIfBlank(query["code"]);
        var state = NullIfBlank(query["state"]);

        if (code is null || state is null)
        {
            return OAuthCallbackResult.Failure("Authorization redirect was missing the required 'code' or 'state' parameter.");
        }

        return OAuthCallbackResult.Success(code, state);
    }

    private static string? NullIfBlank(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value;
}
