namespace FitbitSync.Providers.Fitbit;

public sealed class FitbitOAuthOptions
{
    public string ClientId { get; set; } = "";

    public string? ClientSecret { get; set; }

    public Uri? RedirectUri { get; set; }

    public IReadOnlyList<string> Scopes { get; set; } = new List<string>();

    public TimeSpan RefreshSkew { get; set; } = TimeSpan.FromMinutes(5);

    public Uri TokenEndpoint { get; set; } = new("https://api.fitbit.com/oauth2/token");

    public Uri AuthorizationEndpoint { get; set; } = new("https://www.fitbit.com/oauth2/authorize");
}
