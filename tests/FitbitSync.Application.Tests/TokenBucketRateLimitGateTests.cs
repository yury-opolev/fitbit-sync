namespace FitbitSync.Application.Tests;

// Phase 5 (5d): the rate gate paces every provider call against Fitbit's 150 req/hour/user budget.
// It keeps a conservative local counter, reconciles with the provider's (approximate) rate-limit
// headers taking whichever is MORE restrictive, refuses to consume once exhausted, and — crucially —
// pauses until the reset instant after a 429, refilling automatically when the window rolls over.
// All time is driven by an injected clock so no test ever waits on the wall clock.
public sealed class TokenBucketRateLimitGateTests
{
    private static readonly DateTimeOffset Start = new(2024, 5, 10, 12, 0, 0, TimeSpan.Zero);

    private static (TokenBucketRateLimitGate Gate, TestClock Clock) Build(int budget = 5)
    {
        var clock = new TestClock(Start);
        var options = new SyncOptions { HourlyRequestBudget = budget };
        return (new TokenBucketRateLimitGate(clock, options), clock);
    }

    [Fact]
    public void TryConsume_DecrementsLocalBudget_UntilExhausted()
    {
        var (gate, _) = Build(budget: 3);

        gate.TryConsume().Should().BeTrue();
        gate.TryConsume().Should().BeTrue();
        gate.TryConsume().Should().BeTrue();

        // Fourth call has no budget left.
        gate.TryConsume().Should().BeFalse();
        gate.Remaining.Should().Be(0);
    }

    [Fact]
    public void ApplySnapshot_TakesMoreRestrictiveRemaining()
    {
        var (gate, _) = Build(budget: 100);

        // Provider reports far less remaining than our optimistic local counter: the gate must trust
        // the smaller number (headers are approximate, so we stay conservative).
        gate.ApplySnapshot(new RateLimitSnapshot(2, 150, 3600, Start));

        gate.Remaining.Should().Be(2);
    }

    [Fact]
    public void ApplySnapshot_DoesNotRaiseRemaining_AboveLocalCount()
    {
        var (gate, _) = Build(budget: 3);

        // A rosier provider number must never inflate our budget beyond the local conservative count.
        gate.ApplySnapshot(new RateLimitSnapshot(140, 150, 3600, Start));

        gate.Remaining.Should().Be(3);
    }

    [Fact]
    public void EnterRateLimited_Pauses_UntilSnapshotReset()
    {
        var (gate, clock) = Build();

        gate.EnterRateLimited(new RateLimitSnapshot(0, 150, 900, Start));

        gate.IsPaused.Should().BeTrue();
        gate.PausedUntil.Should().Be(Start.AddSeconds(900));
        gate.TryConsume().Should().BeFalse();

        // Just before the reset: still paused.
        clock.Advance(TimeSpan.FromSeconds(899));
        gate.TryConsume().Should().BeFalse();
    }

    [Fact]
    public void EnterRateLimited_AutoResumes_AfterResetWindow()
    {
        var (gate, clock) = Build(budget: 4);

        gate.EnterRateLimited(new RateLimitSnapshot(0, 150, 900, Start));
        clock.Advance(TimeSpan.FromSeconds(901));

        // Window rolled over: budget refilled and consumption allowed again.
        gate.IsPaused.Should().BeFalse();
        gate.Remaining.Should().Be(4);
        gate.TryConsume().Should().BeTrue();
    }

    [Fact]
    public void EnterRateLimited_WithoutSnapshot_UsesDefaultPause()
    {
        var clock = new TestClock(Start);
        var options = new SyncOptions { HourlyRequestBudget = 5, DefaultRateLimitPause = TimeSpan.FromHours(1) };
        var gate = new TokenBucketRateLimitGate(clock, options);

        gate.EnterRateLimited(null);

        gate.PausedUntil.Should().Be(Start.AddHours(1));
    }

    [Fact]
    public void Window_Refills_AfterOneHour_WithoutRateLimit()
    {
        var (gate, clock) = Build(budget: 2);

        gate.TryConsume();
        gate.TryConsume();
        gate.Remaining.Should().Be(0);

        // An hour after the first consumption the rolling window resets the budget.
        clock.Advance(TimeSpan.FromHours(1).Add(TimeSpan.FromSeconds(1)));
        gate.Remaining.Should().Be(2);
    }
}
