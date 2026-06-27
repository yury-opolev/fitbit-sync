namespace FitbitSync.Domain;

public sealed record OAuthToken(
    string AccessToken,
    string RefreshToken,
    DateTimeOffset ExpiresAt,
    IReadOnlyList<string> Scopes)
{
    public bool IsExpired(DateTimeOffset now) => now >= this.ExpiresAt;
}
