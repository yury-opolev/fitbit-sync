using FluentAssertions;

namespace FitbitSync.Host.Tests;

// Anti-CSRF state comparison. FixedTimeEquals demands equal-length inputs, so differing lengths
// must still return false (not throw) — exercised here.
public sealed class OAuthStateValidatorTests
{
    [Fact]
    public void IsMatch_IdenticalState_ReturnsTrue()
    {
        OAuthStateValidator.IsMatch("s7Hk_3xZ-Q", "s7Hk_3xZ-Q").Should().BeTrue();
    }

    [Fact]
    public void IsMatch_DifferentState_ReturnsFalse()
    {
        OAuthStateValidator.IsMatch("expected-state", "forged-state").Should().BeFalse();
    }

    [Fact]
    public void IsMatch_DifferentLength_ReturnsFalse()
    {
        OAuthStateValidator.IsMatch("short", "a-much-longer-value").Should().BeFalse();
    }

    [Theory]
    [InlineData(null, "x")]
    [InlineData("x", null)]
    [InlineData("", "x")]
    [InlineData("x", "")]
    [InlineData(null, null)]
    public void IsMatch_NullOrEmpty_ReturnsFalse(string? expected, string? returned)
    {
        OAuthStateValidator.IsMatch(expected, returned).Should().BeFalse();
    }

    [Fact]
    public void IsMatch_IsCaseSensitive()
    {
        OAuthStateValidator.IsMatch("AbC", "abc").Should().BeFalse();
    }
}
