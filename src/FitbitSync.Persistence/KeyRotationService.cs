using FitbitSync.Domain;
using FitbitSync.Security;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace FitbitSync.Persistence;

public sealed class KeyRotationService
{
    private const string SchemaMetadataKey = SchemaInitializer.SigningKeyIdMetadataKey;

    private readonly FitbitSyncDbContext dbContext;
    private readonly EncryptedSqliteConnectionFactory connectionFactory;
    private readonly IAuditTrail auditTrail;

    public KeyRotationService(
        FitbitSyncDbContext dbContext,
        EncryptedSqliteConnectionFactory connectionFactory,
        IAuditTrail auditTrail)
    {
        ArgumentNullException.ThrowIfNull(dbContext);
        ArgumentNullException.ThrowIfNull(connectionFactory);
        ArgumentNullException.ThrowIfNull(auditTrail);

        this.dbContext = dbContext;
        this.connectionFactory = connectionFactory;
        this.auditTrail = auditTrail;
    }

    public async Task<KeyRotationResult> RotateAsync(
        IKeyProvider newKeyProvider,
        string? newDatabasePassphrase,
        CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(newKeyProvider);

        var newSigner = new HmacRecordSigner(newKeyProvider);
        var newSigningKeyId = newKeyProvider.SigningKeyId;

        var resignedCount = await this.ResignSamplesAsync(newSigner, newSigningKeyId, ct).ConfigureAwait(false);
        this.UpsertSigningKeyMetadata(newSigningKeyId);

        await this.auditTrail.AppendAsync($"key-rotation:signing-key={newSigningKeyId}", ct).ConfigureAwait(false);

        var rekeyed = false;
        if (!string.IsNullOrWhiteSpace(newDatabasePassphrase))
        {
            var connection = (SqliteConnection)this.dbContext.Database.GetDbConnection();
            this.connectionFactory.Rekey(connection, newDatabasePassphrase);
            rekeyed = true;
        }

        return new KeyRotationResult(resignedCount, newSigningKeyId, rekeyed);
    }

    private async Task<int> ResignSamplesAsync(IRecordSigner newSigner, string newSigningKeyId, CancellationToken ct)
    {
        var rows = await this.dbContext.MetricSamples.ToListAsync(ct).ConfigureAwait(false);

        foreach (var row in rows)
        {
            var sample = MetricSampleMapping.ToDomain(row);
            row.Signature = newSigner.Sign(sample);
            row.SignatureKeyId = newSigningKeyId;
            row.RowVersion = Guid.NewGuid();
        }

        return rows.Count;
    }

    private void UpsertSigningKeyMetadata(string newSigningKeyId)
    {
        var existing = this.dbContext.SchemaMetadata.Find(SchemaMetadataKey);
        if (existing is null)
        {
            this.dbContext.SchemaMetadata.Add(new SchemaMetadataRow { Key = SchemaMetadataKey, Value = newSigningKeyId });
        }
        else
        {
            existing.Value = newSigningKeyId;
        }
    }
}
