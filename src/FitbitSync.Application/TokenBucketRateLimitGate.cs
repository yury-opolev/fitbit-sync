using FitbitSync.Domain;

namespace FitbitSync.Application;

public sealed class TokenBucketRateLimitGate : IRateLimitGate
{
    private readonly IClock clock;
    private readonly SyncOptions options;
    private readonly Lock sync = new();

    private int remaining;
    private DateTimeOffset? windowResetsAt;
    private DateTimeOffset? pausedUntil;

    public TokenBucketRateLimitGate(IClock clock, SyncOptions options)
    {
        ArgumentNullException.ThrowIfNull(clock);
        ArgumentNullException.ThrowIfNull(options);

        this.clock = clock;
        this.options = options;
        this.remaining = options.HourlyRequestBudget;
    }

    public int Remaining
    {
        get
        {
            lock (this.sync)
            {
                this.RefreshWindow();
                return this.remaining;
            }
        }
    }

    public DateTimeOffset? PausedUntil
    {
        get
        {
            lock (this.sync)
            {
                return this.pausedUntil;
            }
        }
    }

    public bool IsPaused
    {
        get
        {
            lock (this.sync)
            {
                return this.pausedUntil is { } until && this.clock.UtcNow < until;
            }
        }
    }

    public bool TryConsume()
    {
        lock (this.sync)
        {
            this.RefreshWindow();

            if (this.pausedUntil is { } until && this.clock.UtcNow < until)
            {
                return false;
            }

            if (this.remaining <= 0)
            {
                return false;
            }

            this.remaining--;
            this.windowResetsAt ??= this.clock.UtcNow.Add(TimeSpan.FromHours(1));
            return true;
        }
    }

    public void ApplySnapshot(RateLimitSnapshot snapshot)
    {
        ArgumentNullException.ThrowIfNull(snapshot);

        lock (this.sync)
        {
            this.RefreshWindow();
            this.remaining = Math.Min(this.remaining, snapshot.Remaining);
            this.windowResetsAt = snapshot.ResetsAt;
        }
    }

    public void EnterRateLimited(RateLimitSnapshot? snapshot)
    {
        lock (this.sync)
        {
            var resumeAt = snapshot?.ResetsAt ?? this.clock.UtcNow.Add(this.options.DefaultRateLimitPause);
            this.remaining = 0;
            this.pausedUntil = resumeAt;
            this.windowResetsAt = resumeAt;
        }
    }

    private void RefreshWindow()
    {
        var now = this.clock.UtcNow;

        if (this.pausedUntil is { } until && now >= until)
        {
            this.pausedUntil = null;
            this.remaining = this.options.HourlyRequestBudget;
            this.windowResetsAt = now.Add(TimeSpan.FromHours(1));
            return;
        }

        if (this.pausedUntil is null && this.windowResetsAt is { } resetsAt && now >= resetsAt)
        {
            this.remaining = this.options.HourlyRequestBudget;
            this.windowResetsAt = now.Add(TimeSpan.FromHours(1));
        }
    }
}
