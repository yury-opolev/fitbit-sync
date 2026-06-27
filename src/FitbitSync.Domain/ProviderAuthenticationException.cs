namespace FitbitSync.Domain;

public class ProviderAuthenticationException : Exception
{
    public ProviderAuthenticationException(string message)
        : base(message)
    {
    }
}
