using FitbitSync.Domain;

namespace FitbitSync.Providers.Fitbit;

public sealed class RateLimitSnapshotHolder
{
    public RateLimitSnapshot? Latest { get; set; }
}
