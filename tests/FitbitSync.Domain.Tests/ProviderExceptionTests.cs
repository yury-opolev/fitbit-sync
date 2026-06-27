namespace FitbitSync.Domain.Tests;

// Phase 5 (5c): the engine lives in the Application layer, which may reference Domain but never the
// Fitbit provider. So "rate limited" and "authentication failed" are modelled as provider-neutral
// Domain exceptions; concrete adapters (Fitbit, and later Google Health) derive from them. The
// rate-limit snapshot rides on the base so the engine can read it without knowing the provider type.
public sealed class ProviderExceptionTests
{
    private static readonly DateTimeOffset ObservedAt = new(2024, 5, 1, 12, 0, 0, TimeSpan.Zero);

    [Fact]
    public void ProviderRateLimitedException_CarriesSnapshot()
    {
        var snapshot = new RateLimitSnapshot(0, 150, 900, ObservedAt);

        var exception = new ProviderRateLimitedException(snapshot);

        exception.RateLimit.Should().BeSameAs(snapshot);
    }

    [Fact]
    public void ProviderRateLimitedException_AllowsNullSnapshot()
    {
        var exception = new ProviderRateLimitedException((RateLimitSnapshot?)null);

        exception.RateLimit.Should().BeNull();
    }

    [Fact]
    public void ProviderAuthenticationException_PreservesMessage()
    {
        var exception = new ProviderAuthenticationException("re-authorization required");

        exception.Message.Should().Be("re-authorization required");
    }
}
