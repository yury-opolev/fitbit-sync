using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace FitbitSync.Persistence.Configurations;

public sealed class SyncCheckpointRowConfiguration : IEntityTypeConfiguration<SyncCheckpointRow>
{
    public void Configure(EntityTypeBuilder<SyncCheckpointRow> builder)
    {
        builder.ToTable("sync_checkpoints");

        builder.HasKey(row => row.Metric);

        builder.Property(row => row.Metric).HasColumnName("metric_type").HasConversion<string>();
        builder.Property(row => row.NewestSynced).HasColumnName("newest_synced");
        builder.Property(row => row.OldestBackfilled).HasColumnName("oldest_backfilled");

        builder.Property(row => row.RowVersion)
            .HasColumnName("row_version")
            .IsConcurrencyToken();
    }
}
