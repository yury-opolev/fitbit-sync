namespace FitbitSync.Application;

public sealed class SyncOptions
{
    public int HourlyRequestBudget { get; set; } = 150;

    public int IncrementalReserve { get; set; } = 20;

    public TimeSpan BackfillWindow { get; set; } = TimeSpan.FromDays(30);

    public TimeSpan Cadence { get; set; } = TimeSpan.FromMinutes(15);

    public TimeSpan DefaultRateLimitPause { get; set; } = TimeSpan.FromHours(1);

    public int MaxRetryAttempts { get; set; } = 3;

    public TimeSpan RetryBaseDelay { get; set; } = TimeSpan.FromSeconds(2);
}
