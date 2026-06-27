using FitbitSync.Domain;

namespace FitbitSync.Application.Tests;

// A hand-controlled IClock for deterministic time in Application tests: advance time explicitly so
// rate-window refills, pause expiry, and scheduler cadence are exercised without any real waiting.
internal sealed class TestClock : IClock
{
    public TestClock(DateTimeOffset start)
    {
        this.UtcNow = start;
    }

    public DateTimeOffset UtcNow { get; private set; }

    public void Advance(TimeSpan by) => this.UtcNow += by;

    public void Set(DateTimeOffset to) => this.UtcNow = to;
}
