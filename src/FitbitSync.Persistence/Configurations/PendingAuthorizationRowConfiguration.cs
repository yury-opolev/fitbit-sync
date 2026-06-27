using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace FitbitSync.Persistence.Configurations;

public sealed class PendingAuthorizationRowConfiguration : IEntityTypeConfiguration<PendingAuthorizationRow>
{
    public void Configure(EntityTypeBuilder<PendingAuthorizationRow> builder)
    {
        builder.ToTable("pending_authorizations");

        builder.HasKey(row => row.Id);

        builder.Property(row => row.Id).HasColumnName("id");
        builder.Property(row => row.State).HasColumnName("state");
        builder.Property(row => row.CodeVerifier).HasColumnName("code_verifier");
        builder.Property(row => row.AuthorizeUrl).HasColumnName("authorize_url");
        builder.Property(row => row.ExpiresAt).HasColumnName("expires_at");
    }
}
