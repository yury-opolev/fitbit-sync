using FitbitSync.Domain;

namespace FitbitSync.Application;

public interface IRateLimitGate
{
    int Remaining { get; }

    DateTimeOffset? PausedUntil { get; }

    bool IsPaused { get; }

    bool TryConsume();

    void ApplySnapshot(RateLimitSnapshot snapshot);

    void EnterRateLimited(RateLimitSnapshot? snapshot);
}
