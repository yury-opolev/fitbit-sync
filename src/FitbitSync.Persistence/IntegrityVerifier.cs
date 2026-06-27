using FitbitSync.Domain;
using FitbitSync.Security;
using Microsoft.EntityFrameworkCore;

namespace FitbitSync.Persistence;

public sealed class IntegrityVerifier : IIntegrityVerifier
{
    private readonly FitbitSyncDbContext dbContext;
    private readonly IAuditTrail auditTrail;
    private readonly IRecordSigner recordSigner;

    public IntegrityVerifier(FitbitSyncDbContext dbContext, IAuditTrail auditTrail, IRecordSigner recordSigner)
    {
        ArgumentNullException.ThrowIfNull(dbContext);
        ArgumentNullException.ThrowIfNull(auditTrail);
        ArgumentNullException.ThrowIfNull(recordSigner);

        this.dbContext = dbContext;
        this.auditTrail = auditTrail;
        this.recordSigner = recordSigner;
    }

    public async Task<IntegrityReport> VerifyAsync(CancellationToken ct = default)
    {
        var isAuditChainIntact = await this.auditTrail.VerifyChainAsync(ct).ConfigureAwait(false);

        var rows = await this.dbContext.MetricSamples
            .OrderBy(row => row.Id)
            .ToListAsync(ct)
            .ConfigureAwait(false);

        var verifiedCount = 0;
        var forgedCount = 0;

        foreach (var row in rows)
        {
            var sample = MetricSampleMapping.ToDomain(row);
            if (this.recordSigner.Verify(sample, row.Signature))
            {
                verifiedCount++;
            }
            else
            {
                forgedCount++;
            }
        }

        return new IntegrityReport(isAuditChainIntact, verifiedCount, forgedCount);
    }
}
