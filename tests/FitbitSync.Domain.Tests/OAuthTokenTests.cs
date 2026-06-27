namespace FitbitSync.Domain.Tests;

// OAuthToken carries the access/refresh pair and an absolute expiry. IsExpired is the
// proactive/reactive refresh signal; expiry is treated as inclusive (at the instant of
// expiry the token is already considered expired).
public sealed class OAuthTokenTests
{
    private static readonly DateTimeOffset ExpiresAt = new(2024, 5, 1, 12, 0, 0, TimeSpan.Zero);

    private static OAuthToken CreateToken() =>
        new("access", "refresh", ExpiresAt, ["activity", "heartrate"]);

    [Fact]
    public void IsExpired_False_BeforeExpiry()
    {
        // Given the current time is one second before expiry...
        var token = CreateToken();

        token.IsExpired(ExpiresAt.AddSeconds(-1)).Should().BeFalse();
    }

    [Fact]
    public void IsExpired_True_AtExpiryInstant()
    {
        // Given the current time is exactly the expiry instant (inclusive)...
        var token = CreateToken();

        token.IsExpired(ExpiresAt).Should().BeTrue();
    }

    [Fact]
    public void IsExpired_True_AfterExpiry()
    {
        // Given the current time is past expiry...
        var token = CreateToken();

        token.IsExpired(ExpiresAt.AddSeconds(1)).Should().BeTrue();
    }

    [Fact]
    public void Tokens_WithSameValues_AreEqual()
    {
        // Given two tokens constructed with identical field values...
        var first = new OAuthToken("access", "refresh", ExpiresAt, ["activity"]);
        var second = new OAuthToken("access", "refresh", ExpiresAt, ["activity"]);

        // Then record value-equality holds for the scalar fields.
        first.AccessToken.Should().Be(second.AccessToken);
        first.RefreshToken.Should().Be(second.RefreshToken);
        first.ExpiresAt.Should().Be(second.ExpiresAt);
    }
}
