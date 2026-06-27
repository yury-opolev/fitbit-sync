using System.Security.Cryptography;
using System.Text;
using FitbitSync.Domain;
using FitbitSync.Persistence;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;

namespace FitbitSync.Persistence.Tests;

// Phase 2 security properties for the OAuth token store: a round-trip through the encrypted column
// cipher must preserve the token, the at-rest BLOB columns must never expose the plaintext, and the
// per-column AAD binding must reject a ciphertext decrypted under the wrong column's associated data.
// Each operation uses a FRESH DbContext so the encrypted database — not one tracker — is exercised.
public sealed class TokenStoreTests : IDisposable
{
    private readonly EncryptedDatabaseFixture fixture = new();

    // The AAD contract enforced by TokenStore (bind each ciphertext to its column + provider).
    private static readonly byte[] AccessTokenAssociatedData =
        Encoding.UTF8.GetBytes("oauth_tokens:access_token:fitbit");

    private static readonly byte[] RefreshTokenAssociatedData =
        Encoding.UTF8.GetBytes("oauth_tokens:refresh_token:fitbit");

    private const string AccessTokenPlaintext = "ACCESS-PLAINTEXT-NEEDLE-aaaa1111";
    private const string RefreshTokenPlaintext = "REFRESH-PLAINTEXT-NEEDLE-bbbb2222";

    private static readonly DateTimeOffset ExpiresAt = new(2024, 5, 1, 12, 0, 0, TimeSpan.Zero);

    private async Task SaveThroughFreshContextAsync(OAuthToken token)
    {
        using var context = this.fixture.NewDbContext();
        var store = new TokenStore(context, this.fixture.ColumnCipher);
        await store.SaveAsync(token);
    }

    private (byte[] AccessCipher, byte[] RefreshCipher) ReadRawCipherColumns()
    {
        using var context = this.fixture.NewDbContext();
        var connection = context.Database.GetDbConnection();
        using var command = connection.CreateCommand();
        command.CommandText = "SELECT access_token_cipher, refresh_token_cipher FROM oauth_tokens WHERE id = 1;";
        using var reader = command.ExecuteReader();
        reader.Read().Should().BeTrue();
        return ((byte[])reader["access_token_cipher"], (byte[])reader["refresh_token_cipher"]);
    }

    [Fact]
    public async Task TokenStore_SaveThenLoad_RoundTripsToken()
    {
        // Given a token persisted through the encrypted cipher...
        var token = new OAuthToken(
            AccessTokenPlaintext,
            RefreshTokenPlaintext,
            ExpiresAt,
            ["activity", "heartrate", "sleep"]);

        await this.SaveThroughFreshContextAsync(token);

        // When reloaded through a fresh context, every field is recovered intact.
        using var context = this.fixture.NewDbContext();
        var store = new TokenStore(context, this.fixture.ColumnCipher);
        var loaded = await store.LoadAsync();

        loaded.Should().NotBeNull();
        loaded!.AccessToken.Should().Be(AccessTokenPlaintext);
        loaded.RefreshToken.Should().Be(RefreshTokenPlaintext);
        loaded.ExpiresAt.Should().Be(ExpiresAt);
        loaded.Scopes.Should().Equal("activity", "heartrate", "sleep");
    }

    [Fact]
    public async Task TokenStore_AtRestCipherColumns_DoNotContainPlaintext()
    {
        // Given a saved token...
        var token = new OAuthToken(AccessTokenPlaintext, RefreshTokenPlaintext, ExpiresAt, ["activity"]);
        await this.SaveThroughFreshContextAsync(token);

        // When reading the raw BLOB columns directly, neither plaintext token appears in the bytes.
        var (accessCipher, refreshCipher) = this.ReadRawCipherColumns();

        var accessNeedle = Encoding.UTF8.GetBytes(AccessTokenPlaintext);
        var refreshNeedle = Encoding.UTF8.GetBytes(RefreshTokenPlaintext);

        Contains(accessCipher, accessNeedle).Should().BeFalse();
        Contains(accessCipher, refreshNeedle).Should().BeFalse();
        Contains(refreshCipher, accessNeedle).Should().BeFalse();
        Contains(refreshCipher, refreshNeedle).Should().BeFalse();
    }

    [Fact]
    public async Task TokenStore_DecryptingAccessCipherUnderRefreshAad_FailsAuthentication()
    {
        // Given a saved token whose access column ciphertext is bound to the access-token AAD...
        var token = new OAuthToken(AccessTokenPlaintext, RefreshTokenPlaintext, ExpiresAt, ["activity"]);
        await this.SaveThroughFreshContextAsync(token);

        var (accessCipher, _) = this.ReadRawCipherColumns();

        // When decrypting it under the REFRESH column's AAD, GCM authentication must reject it...
        var swapAad = () => this.fixture.ColumnCipher.Decrypt(accessCipher, RefreshTokenAssociatedData);
        swapAad.Should().Throw<CryptographicException>();

        // ...while decrypting under the correct AAD still succeeds (proves the cipher itself is sound).
        var correct = this.fixture.ColumnCipher.Decrypt(accessCipher, AccessTokenAssociatedData);
        Encoding.UTF8.GetString(correct).Should().Be(AccessTokenPlaintext);
    }

    private static bool Contains(byte[] haystack, byte[] needle)
    {
        for (var i = 0; i <= haystack.Length - needle.Length; i++)
        {
            var found = true;
            for (var j = 0; j < needle.Length; j++)
            {
                if (haystack[i + j] != needle[j])
                {
                    found = false;
                    break;
                }
            }

            if (found)
            {
                return true;
            }
        }

        return false;
    }

    public void Dispose() => this.fixture.Dispose();
}
