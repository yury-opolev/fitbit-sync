namespace FitbitSync.Domain;

public class ProviderRateLimitedException : Exception
{
    public ProviderRateLimitedException(RateLimitSnapshot? rateLimit)
        : base("The health-data provider returned a rate-limit response.")
    {
        this.RateLimit = rateLimit;
    }

    public ProviderRateLimitedException(string message, RateLimitSnapshot? rateLimit)
        : base(message)
    {
        this.RateLimit = rateLimit;
    }

    public RateLimitSnapshot? RateLimit { get; }
}
