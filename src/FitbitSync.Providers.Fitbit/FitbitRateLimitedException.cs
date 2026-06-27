using FitbitSync.Domain;

namespace FitbitSync.Providers.Fitbit;

public sealed class FitbitRateLimitedException : ProviderRateLimitedException
{
    public FitbitRateLimitedException(RateLimitSnapshot? rateLimit)
        : base("Fitbit returned 429 Too Many Requests.", rateLimit)
    {
    }
}
