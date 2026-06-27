namespace FitbitSync.Domain;

public sealed record RateLimitSnapshot(
    int Remaining,
    int Limit,
    int ResetSeconds,
    DateTimeOffset ObservedAt)
{
    public bool IsExhausted => this.Remaining <= 0;

    public DateTimeOffset ResetsAt => this.ObservedAt.AddSeconds(this.ResetSeconds);
}
