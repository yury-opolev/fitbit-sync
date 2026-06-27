using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace FitbitSync.Persistence.Configurations;

public sealed class OAuthTokenRowConfiguration : IEntityTypeConfiguration<OAuthTokenRow>
{
    public void Configure(EntityTypeBuilder<OAuthTokenRow> builder)
    {
        builder.ToTable("oauth_tokens");

        builder.HasKey(row => row.Id);

        builder.Property(row => row.Id).HasColumnName("id");
        builder.Property(row => row.AccessTokenCipher).HasColumnName("access_token_cipher");
        builder.Property(row => row.RefreshTokenCipher).HasColumnName("refresh_token_cipher");
        builder.Property(row => row.ExpiresAt).HasColumnName("expires_at");
        builder.Property(row => row.ScopeCsv).HasColumnName("scope_csv");

        builder.Property(row => row.RowVersion)
            .HasColumnName("row_version")
            .IsConcurrencyToken();
    }
}
