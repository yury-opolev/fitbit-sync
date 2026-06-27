using FitbitSync.Domain;

namespace FitbitSync.Providers.Fitbit;

public sealed class FitbitAuthenticationException : ProviderAuthenticationException
{
    public FitbitAuthenticationException(string message)
        : base(message)
    {
    }
}
