using FitbitSync.Domain;

namespace FitbitSync.Providers.GoogleHealth;

public sealed class GoogleAuthenticationException : ProviderAuthenticationException
{
    public GoogleAuthenticationException(string message)
        : base(message)
    {
    }
}
