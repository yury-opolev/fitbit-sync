namespace FitbitSync.Persistence;

public sealed class OAuthTokenRow
{
    public int Id { get; set; }

    public byte[] AccessTokenCipher { get; set; } = [];

    public byte[] RefreshTokenCipher { get; set; } = [];

    public DateTimeOffset ExpiresAt { get; set; }

    public string ScopeCsv { get; set; } = string.Empty;

    public Guid RowVersion { get; set; }
}
