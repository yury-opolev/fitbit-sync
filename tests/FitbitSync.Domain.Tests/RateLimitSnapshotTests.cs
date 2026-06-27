namespace FitbitSync.Domain.Tests;

// RateLimitSnapshot models the Fitbit rate-limit headers. ResetSeconds is "seconds until
// reset" (not an absolute timestamp), so ResetsAt converts it to an absolute instant
// relative to when the snapshot was observed.
public sealed class RateLimitSnapshotTests
{
    private static readonly DateTimeOffset ObservedAt = new(2024, 5, 1, 12, 0, 0, TimeSpan.Zero);

    [Fact]
    public void IsExhausted_True_WhenRemainingIsZero()
    {
        // Given a snapshot with no remaining budget...
        var snapshot = new RateLimitSnapshot(0, 150, 3600, ObservedAt);

        snapshot.IsExhausted.Should().BeTrue();
    }

    [Fact]
    public void IsExhausted_True_WhenRemainingIsNegative()
    {
        // Given a snapshot that somehow overshot (defensive: treat as exhausted)...
        var snapshot = new RateLimitSnapshot(-1, 150, 3600, ObservedAt);

        snapshot.IsExhausted.Should().BeTrue();
    }

    [Fact]
    public void IsExhausted_False_WhenBudgetRemains()
    {
        // Given a snapshot with budget still available...
        var snapshot = new RateLimitSnapshot(42, 150, 3600, ObservedAt);

        snapshot.IsExhausted.Should().BeFalse();
    }

    [Fact]
    public void ResetsAt_Equals_ObservedAt_Plus_ResetSeconds()
    {
        // Given a snapshot observed at a known instant with a seconds-until-reset value...
        var snapshot = new RateLimitSnapshot(10, 150, 3600, ObservedAt);

        // Then ResetsAt is that instant plus the reset window.
        snapshot.ResetsAt.Should().Be(ObservedAt.AddSeconds(3600));
    }
}
