using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace FitbitSync.Persistence.Configurations;

public sealed class SchemaMetadataRowConfiguration : IEntityTypeConfiguration<SchemaMetadataRow>
{
    public void Configure(EntityTypeBuilder<SchemaMetadataRow> builder)
    {
        builder.ToTable("schema_metadata");

        builder.HasKey(row => row.Key);

        builder.Property(row => row.Key).HasColumnName("key");
        builder.Property(row => row.Value).HasColumnName("value");
    }
}
