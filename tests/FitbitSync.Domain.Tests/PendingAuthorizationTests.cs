namespace FitbitSync.Domain.Tests;

// PendingAuthorization holds the in-flight PKCE login (state + verifier + authorize URL) plus an
// absolute expiry, persisted between `login --begin` and `login --complete`. IsExpired mirrors
// OAuthToken: expiry is inclusive (at the expiry instant the pending login is already expired and
// must be rejected on completion).
public sealed class PendingAuthorizationTests
{
    private static readonly DateTimeOffset ExpiresAt = new(2024, 5, 1, 12, 0, 0, TimeSpan.Zero);

    private static PendingAuthorization Create() =>
        new("state-abc", "verifier-xyz", new Uri("https://www.fitbit.com/oauth2/authorize?response_type=code"), ExpiresAt);

    [Fact]
    public void IsExpired_False_BeforeExpiry()
    {
        Create().IsExpired(ExpiresAt.AddSeconds(-1)).Should().BeFalse();
    }

    [Fact]
    public void IsExpired_True_AtExpiryInstant()
    {
        Create().IsExpired(ExpiresAt).Should().BeTrue();
    }

    [Fact]
    public void IsExpired_True_AfterExpiry()
    {
        Create().IsExpired(ExpiresAt.AddSeconds(1)).Should().BeTrue();
    }
}
