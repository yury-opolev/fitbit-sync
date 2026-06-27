using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace FitbitSync.Persistence.Configurations;

public sealed class MetricSampleRowConfiguration : IEntityTypeConfiguration<MetricSampleRow>
{
    public void Configure(EntityTypeBuilder<MetricSampleRow> builder)
    {
        builder.ToTable("metric_samples");

        builder.HasKey(row => row.Id);

        builder.Property(row => row.Id).HasColumnName("id");
        builder.Property(row => row.Type).HasColumnName("metric_type").HasConversion<string>();
        builder.Property(row => row.Timestamp).HasColumnName("timestamp");
        builder.Property(row => row.Value).HasColumnName("value");
        builder.Property(row => row.Unit).HasColumnName("unit");
        builder.Property(row => row.Resolution).HasColumnName("resolution").HasConversion<string>();
        builder.Property(row => row.Source).HasColumnName("source");
        builder.Property(row => row.Signature).HasColumnName("signature");
        builder.Property(row => row.SignatureKeyId).HasColumnName("signature_key_id");

        builder.Property(row => row.RowVersion)
            .HasColumnName("row_version")
            .IsConcurrencyToken();

        builder.HasIndex(row => new { row.Source, row.Type, row.Resolution, row.Timestamp })
            .IsUnique()
            .HasDatabaseName("ux_metric_samples_idempotency");
    }
}
