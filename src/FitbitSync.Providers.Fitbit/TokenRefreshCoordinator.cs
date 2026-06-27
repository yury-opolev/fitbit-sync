using System.Collections.Concurrent;

namespace FitbitSync.Providers.Fitbit;

public sealed class TokenRefreshCoordinator : ITokenRefreshCoordinator
{
    private readonly ConcurrentDictionary<string, SemaphoreSlim> gates = new();

    public async Task<T> RunSingleFlightAsync<T>(string key, Func<CancellationToken, Task<T>> action, CancellationToken ct)
    {
        var gate = this.gates.GetOrAdd(key, _ => new SemaphoreSlim(1, 1));
        await gate.WaitAsync(ct).ConfigureAwait(false);
        try
        {
            return await action(ct).ConfigureAwait(false);
        }
        finally
        {
            gate.Release();
        }
    }
}
