using FitbitSync.Domain;
using FitbitSync.Security;
using Microsoft.EntityFrameworkCore;

namespace FitbitSync.Persistence;

public sealed class MetricRepository : IMetricRepository
{
    private readonly FitbitSyncDbContext dbContext;
    private readonly IRecordSigner recordSigner;
    private readonly IKeyProvider keyProvider;

    public MetricRepository(FitbitSyncDbContext dbContext, IRecordSigner recordSigner, IKeyProvider keyProvider)
    {
        ArgumentNullException.ThrowIfNull(dbContext);
        ArgumentNullException.ThrowIfNull(recordSigner);
        ArgumentNullException.ThrowIfNull(keyProvider);

        this.dbContext = dbContext;
        this.recordSigner = recordSigner;
        this.keyProvider = keyProvider;
    }

    public async Task UpsertAsync(IReadOnlyCollection<MetricSample> samples, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(samples);

        foreach (var sample in samples)
        {
            var signature = this.recordSigner.Sign(sample);
            var signatureKeyId = this.keyProvider.SigningKeyId;

            var existing = await this.dbContext.MetricSamples
                .SingleOrDefaultAsync(
                    row => row.Source == sample.Source
                        && row.Type == sample.Type
                        && row.Resolution == sample.Resolution
                        && row.Timestamp == sample.Timestamp,
                    ct)
                .ConfigureAwait(false);

            if (existing is null)
            {
                this.dbContext.MetricSamples.Add(MetricSampleMapping.ToRow(sample, signature, signatureKeyId));
            }
            else
            {
                existing.Value = sample.Value;
                existing.Unit = sample.Unit;
                existing.Signature = signature;
                existing.SignatureKeyId = signatureKeyId;
                existing.RowVersion = Guid.NewGuid();
            }
        }

        await this.dbContext.SaveChangesAsync(ct).ConfigureAwait(false);
    }

    public async Task<SyncCheckpoint?> GetCheckpointAsync(MetricType metric, CancellationToken ct = default)
    {
        var row = await this.dbContext.SyncCheckpoints
            .SingleOrDefaultAsync(checkpoint => checkpoint.Metric == metric, ct)
            .ConfigureAwait(false);

        return row is null ? null : SyncCheckpointMapping.ToDomain(row);
    }

    public async Task<IReadOnlyList<DateOnly>> GetCoveredDatesAsync(MetricType metric, DateRange range, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(range);

        var timestamps = await this.QueryWindowAsync(metric, range, ct).ConfigureAwait(false);

        return timestamps
            .Select(timestamp => DateOnly.FromDateTime(timestamp.UtcDateTime))
            .Distinct()
            .OrderBy(date => date)
            .ToList();
    }

    public async Task<IReadOnlyList<MetricSample>> QueryAsync(MetricType metric, DateRange range, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(range);

        var rows = await this.dbContext.MetricSamples
            .Where(row => row.Type == metric)
            .ToListAsync(ct)
            .ConfigureAwait(false);

        return rows
            .Where(row => InRange(row.Timestamp, range))
            .OrderBy(row => row.Timestamp)
            .Select(MetricSampleMapping.ToDomain)
            .ToList();
    }

    private async Task<IReadOnlyList<DateTimeOffset>> QueryWindowAsync(MetricType metric, DateRange range, CancellationToken ct)
    {
        var timestamps = await this.dbContext.MetricSamples
            .Where(row => row.Type == metric)
            .Select(row => row.Timestamp)
            .ToListAsync(ct)
            .ConfigureAwait(false);

        return timestamps.Where(timestamp => InRange(timestamp, range)).ToList();
    }

    private static bool InRange(DateTimeOffset timestamp, DateRange range)
    {
        var date = DateOnly.FromDateTime(timestamp.UtcDateTime);
        return date >= range.Start && date <= range.End;
    }
}
