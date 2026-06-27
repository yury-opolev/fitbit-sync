namespace FitbitSync.Providers.GoogleHealth;

public sealed class GoogleOAuthOptions
{
    public string ClientId { get; set; } = "";

    public string ClientSecret { get; set; } = "";

    public Uri? RedirectUri { get; set; }

    public IReadOnlyList<string> Scopes { get; set; } = new List<string>();

    public TimeSpan RefreshSkew { get; set; } = TimeSpan.FromMinutes(5);

    public Uri AuthorizationEndpoint { get; set; } = new("https://accounts.google.com/o/oauth2/v2/auth");

    public Uri TokenEndpoint { get; set; } = new("https://oauth2.googleapis.com/token");
}
