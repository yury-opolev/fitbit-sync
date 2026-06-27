namespace FitbitSync.Domain;

public sealed record IntegrityReport(
    bool IsAuditChainIntact,
    int VerifiedSampleCount,
    int ForgedSampleCount)
{
    public bool IsValid => this.IsAuditChainIntact && this.ForgedSampleCount == 0;
}
