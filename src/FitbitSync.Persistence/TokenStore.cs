using System.Text;
using FitbitSync.Domain;
using FitbitSync.Security;
using Microsoft.EntityFrameworkCore;

namespace FitbitSync.Persistence;

public sealed class TokenStore : ITokenStore
{
    private const int SingleRowId = 1;

    private static readonly byte[] AccessTokenAssociatedData =
        Encoding.UTF8.GetBytes("oauth_tokens:access_token:fitbit");

    private static readonly byte[] RefreshTokenAssociatedData =
        Encoding.UTF8.GetBytes("oauth_tokens:refresh_token:fitbit");

    private readonly FitbitSyncDbContext dbContext;
    private readonly IColumnCipher columnCipher;

    public TokenStore(FitbitSyncDbContext dbContext, IColumnCipher columnCipher)
    {
        ArgumentNullException.ThrowIfNull(dbContext);
        ArgumentNullException.ThrowIfNull(columnCipher);

        this.dbContext = dbContext;
        this.columnCipher = columnCipher;
    }

    public async Task<OAuthToken?> LoadAsync(CancellationToken ct = default)
    {
        var row = await this.dbContext.OAuthTokens
            .SingleOrDefaultAsync(token => token.Id == SingleRowId, ct)
            .ConfigureAwait(false);

        if (row is null)
        {
            return null;
        }

        var accessToken = this.Decrypt(row.AccessTokenCipher, AccessTokenAssociatedData);
        var refreshToken = this.Decrypt(row.RefreshTokenCipher, RefreshTokenAssociatedData);

        return new OAuthToken(accessToken, refreshToken, row.ExpiresAt, SplitScopes(row.ScopeCsv));
    }

    public async Task SaveAsync(OAuthToken token, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(token);

        var accessTokenCipher = this.Encrypt(token.AccessToken, AccessTokenAssociatedData);
        var refreshTokenCipher = this.Encrypt(token.RefreshToken, RefreshTokenAssociatedData);
        var scopeCsv = JoinScopes(token.Scopes);

        var existing = await this.dbContext.OAuthTokens
            .SingleOrDefaultAsync(row => row.Id == SingleRowId, ct)
            .ConfigureAwait(false);

        if (existing is null)
        {
            this.dbContext.OAuthTokens.Add(new OAuthTokenRow
            {
                Id = SingleRowId,
                AccessTokenCipher = accessTokenCipher,
                RefreshTokenCipher = refreshTokenCipher,
                ExpiresAt = token.ExpiresAt,
                ScopeCsv = scopeCsv,
                RowVersion = Guid.NewGuid(),
            });
        }
        else
        {
            existing.AccessTokenCipher = accessTokenCipher;
            existing.RefreshTokenCipher = refreshTokenCipher;
            existing.ExpiresAt = token.ExpiresAt;
            existing.ScopeCsv = scopeCsv;
            existing.RowVersion = Guid.NewGuid();
        }

        await this.dbContext.SaveChangesAsync(ct).ConfigureAwait(false);
    }

    private byte[] Encrypt(string plaintext, byte[] associatedData) =>
        this.columnCipher.Encrypt(Encoding.UTF8.GetBytes(plaintext), associatedData);

    private string Decrypt(byte[] envelope, byte[] associatedData) =>
        Encoding.UTF8.GetString(this.columnCipher.Decrypt(envelope, associatedData));

    private static string JoinScopes(IReadOnlyList<string> scopes) => string.Join(',', scopes);

    private static IReadOnlyList<string> SplitScopes(string scopeCsv) =>
        scopeCsv.Length == 0 ? [] : scopeCsv.Split(',');
}
