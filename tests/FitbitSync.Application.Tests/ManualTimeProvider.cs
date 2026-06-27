namespace FitbitSync.Application.Tests;

// A minimal controllable TimeProvider for scheduler tests: it captures the timer PeriodicTimer creates and
// lets the test fire ticks on demand (FireTimer), so the 15-minute cadence is exercised with zero real
// waiting. Microsoft.Extensions.Time.Testing.FakeTimeProvider is not available in this offline feed, so we
// supply just enough surface for PeriodicTimer (CreateTimer + a fired callback).
internal sealed class ManualTimeProvider : TimeProvider
{
    private readonly List<FiredTimer> timers = [];

    public int TimerCount => this.timers.Count;

    public void FireAll()
    {
        foreach (var timer in this.timers.ToArray())
        {
            timer.Fire();
        }
    }

    public override ITimer CreateTimer(TimerCallback callback, object? state, TimeSpan dueTime, TimeSpan period)
    {
        var timer = new FiredTimer(callback, state);
        this.timers.Add(timer);
        return timer;
    }

    private sealed class FiredTimer : ITimer
    {
        private readonly TimerCallback callback;
        private readonly object? state;

        public FiredTimer(TimerCallback callback, object? state)
        {
            this.callback = callback;
            this.state = state;
        }

        public void Fire() => this.callback(this.state);

        public bool Change(TimeSpan dueTime, TimeSpan period) => true;

        public void Dispose()
        {
        }

        public ValueTask DisposeAsync() => ValueTask.CompletedTask;
    }
}
