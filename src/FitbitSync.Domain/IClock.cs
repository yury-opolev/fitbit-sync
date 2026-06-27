namespace FitbitSync.Domain;

public interface IClock
{
    DateTimeOffset UtcNow { get; }
}
