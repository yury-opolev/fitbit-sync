namespace FitbitSync.Providers.Fitbit;

public interface ITokenRefreshCoordinator
{
    Task<T> RunSingleFlightAsync<T>(string key, Func<CancellationToken, Task<T>> action, CancellationToken ct);
}
