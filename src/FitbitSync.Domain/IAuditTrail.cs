namespace FitbitSync.Domain;

public interface IAuditTrail
{
    Task<AuditEntry> AppendAsync(string action, CancellationToken ct = default);

    Task<bool> VerifyChainAsync(CancellationToken ct = default);
}
