using Microsoft.EntityFrameworkCore;

namespace FitbitSync.Persistence;

public sealed class FitbitSyncDbContext : DbContext
{
    public FitbitSyncDbContext(DbContextOptions<FitbitSyncDbContext> options)
        : base(options)
    {
    }

    public DbSet<MetricSampleRow> MetricSamples => this.Set<MetricSampleRow>();

    public DbSet<OAuthTokenRow> OAuthTokens => this.Set<OAuthTokenRow>();

    public DbSet<SyncCheckpointRow> SyncCheckpoints => this.Set<SyncCheckpointRow>();

    public DbSet<AuditEntryRow> AuditEntries => this.Set<AuditEntryRow>();

    public DbSet<SchemaMetadataRow> SchemaMetadata => this.Set<SchemaMetadataRow>();

    public override int SaveChanges(bool acceptAllChangesOnSuccess)
    {
        this.GuardAuditAppendOnly();
        return base.SaveChanges(acceptAllChangesOnSuccess);
    }

    public override Task<int> SaveChangesAsync(bool acceptAllChangesOnSuccess, CancellationToken cancellationToken = default)
    {
        this.GuardAuditAppendOnly();
        return base.SaveChangesAsync(acceptAllChangesOnSuccess, cancellationToken);
    }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(FitbitSyncDbContext).Assembly);
    }

    private void GuardAuditAppendOnly()
    {
        foreach (var entry in this.ChangeTracker.Entries<AuditEntryRow>())
        {
            if (entry.State is EntityState.Modified or EntityState.Deleted)
            {
                throw new InvalidOperationException("Audit entries are append-only; modification and deletion are forbidden.");
            }
        }
    }
}
