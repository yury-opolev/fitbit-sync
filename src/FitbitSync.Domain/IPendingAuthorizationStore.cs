namespace FitbitSync.Domain;

// Persists the single in-flight PKCE login between `login --begin` and `login --complete`. At most
// one pending authorization exists at a time: a new login replaces any prior one, and completion
// (or expiry) clears it.
public interface IPendingAuthorizationStore
{
    Task SaveAsync(PendingAuthorization pending, CancellationToken ct = default);

    Task<PendingAuthorization?> GetAsync(CancellationToken ct = default);

    Task DeleteAsync(CancellationToken ct = default);
}
