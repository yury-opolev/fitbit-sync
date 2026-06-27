namespace FitbitSync.Domain;

public interface IIntegrityVerifier
{
    Task<IntegrityReport> VerifyAsync(CancellationToken ct = default);
}
