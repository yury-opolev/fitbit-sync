using System.Security.Cryptography;
using System.Text;
using FitbitSync.Security;
using FluentAssertions;

namespace FitbitSync.Security.Tests;

// Phase 1 / Increment 2: the key provider is the single seam through which the rest of the
// system obtains symmetric key material. The in-memory implementation is the dev/test key
// source; a real OS-protected origin (DPAPI/file/KMS) replaces it behind IKeyProvider later.
public sealed class InMemoryKeyProviderTests
{
    private static readonly byte[] ColumnKey = RandomNumberGenerator.GetBytes(32);
    private static readonly byte[] SigningKey = RandomNumberGenerator.GetBytes(32);

    [Fact]
    public void Provider_ReturnsKeysOfExpectedLength()
    {
        // Given a provider seeded with 256-bit keys...
        var provider = new InMemoryKeyProvider(ColumnKey, SigningKey);

        // Then both keys are 32 bytes (256-bit), the length AES-256 / HMAC-SHA256 expect.
        provider.GetColumnEncryptionKey().Length.Should().Be(32);
        provider.GetSigningKey().Length.Should().Be(32);
    }

    [Fact]
    public void Provider_ReturnsStableKeys_AcrossCalls()
    {
        // Given a single provider instance...
        var provider = new InMemoryKeyProvider(ColumnKey, SigningKey);

        // When the same key is requested twice, the value is stable (deterministic).
        provider.GetColumnEncryptionKey().ToArray().Should().Equal(provider.GetColumnEncryptionKey().ToArray());

        // And the returned material matches what was supplied at construction.
        provider.GetSigningKey().ToArray().Should().Equal(SigningKey);
    }

    [Fact]
    public void Provider_RejectsKeyOfWrongLength()
    {
        // Given a key that is not 256-bit...
        var tooShort = new byte[16];

        // When constructing the provider, it fails fast rather than yielding a weak key.
        var construct = () => new InMemoryKeyProvider(tooShort, SigningKey);

        construct.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void ColumnCipher_EncryptsAndDecrypts_UsingKeyFromProvider()
    {
        // Given a cipher keyed entirely from the provider seam (no hard-coded key)...
        var provider = new InMemoryKeyProvider(ColumnKey, SigningKey);
        var cipher = new AesGcmColumnCipher(provider.GetColumnEncryptionKey().ToArray());
        var associatedData = Encoding.UTF8.GetBytes("oauth_tokens:refresh_token_cipher:fitbit");
        var secret = Encoding.UTF8.GetBytes("refresh-token-value");

        // When round-tripping through the cipher...
        var envelope = cipher.Encrypt(secret, associatedData);
        var roundTripped = cipher.Decrypt(envelope, associatedData);

        // Then the secret is recovered, proving the provider supplies a usable AES key.
        roundTripped.Should().Equal(secret);
    }
}
