namespace FitbitSync.Domain;

public interface ITokenStore
{
    Task<OAuthToken?> LoadAsync(CancellationToken ct = default);

    Task SaveAsync(OAuthToken token, CancellationToken ct = default);
}
