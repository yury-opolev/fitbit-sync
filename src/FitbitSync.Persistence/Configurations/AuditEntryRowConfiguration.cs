using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace FitbitSync.Persistence.Configurations;

public sealed class AuditEntryRowConfiguration : IEntityTypeConfiguration<AuditEntryRow>
{
    public void Configure(EntityTypeBuilder<AuditEntryRow> builder)
    {
        builder.ToTable("audit_entries");

        builder.HasKey(row => row.Sequence);

        builder.Property(row => row.Sequence).HasColumnName("sequence").ValueGeneratedNever();
        builder.Property(row => row.Timestamp).HasColumnName("timestamp");
        builder.Property(row => row.Action).HasColumnName("action");
        builder.Property(row => row.PrevHash).HasColumnName("prev_hash");
        builder.Property(row => row.EntryHash).HasColumnName("entry_hash");
    }
}
