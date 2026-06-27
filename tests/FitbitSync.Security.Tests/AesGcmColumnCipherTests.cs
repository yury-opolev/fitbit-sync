using System.Security.Cryptography;
using System.Text;
using FitbitSync.Security;
using FluentAssertions;

namespace FitbitSync.Security.Tests;

// Phase 1 / Increment 1: the AES-GCM column cipher must round-trip plaintext and,
// because it is authenticated (AEAD), MUST reject any tampering with either the
// ciphertext envelope or the associated data that binds a value to its row identity.
public sealed class AesGcmColumnCipherTests
{
    private static readonly byte[] Key = RandomNumberGenerator.GetBytes(32);

    private static byte[] Plaintext(string value) => Encoding.UTF8.GetBytes(value);

    [Fact]
    public void AesGcmColumnCipher_RoundTrips_WithMatchingAssociatedData()
    {
        // Given a cipher and a row-binding associated data value...
        var cipher = new AesGcmColumnCipher(Key);
        var associatedData = Plaintext("oauth_tokens:access_token_cipher:fitbit");
        var secret = Plaintext("super-secret-access-token");

        // When encrypting then decrypting with the SAME associated data...
        var envelope = cipher.Encrypt(secret, associatedData);
        var roundTripped = cipher.Decrypt(envelope, associatedData);

        // Then the original plaintext is recovered, and the envelope is not the plaintext.
        roundTripped.Should().Equal(secret);
        envelope.Should().NotEqual(secret);
    }

    [Fact]
    public void AesGcmColumnCipher_RoundTrips_AndRejectsTamperedAssociatedData()
    {
        // Given an encrypted value bound to a specific row's associated data...
        var cipher = new AesGcmColumnCipher(Key);
        var associatedData = Plaintext("oauth_tokens:access_token_cipher:fitbit");
        var secret = Plaintext("super-secret-access-token");
        var envelope = cipher.Encrypt(secret, associatedData);

        // Sanity: the happy path round-trips.
        cipher.Decrypt(envelope, associatedData).Should().Equal(secret);

        // When the ciphertext envelope is mutated (flip one byte of the ciphertext body)...
        var tamperedEnvelope = (byte[])envelope.Clone();
        tamperedEnvelope[^1] ^= 0xFF;
        var decryptTamperedCiphertext = () => cipher.Decrypt(tamperedEnvelope, associatedData);

        // Then authentication fails.
        decryptTamperedCiphertext.Should().Throw<CryptographicException>();

        // And when the associated data is swapped (a copy/paste-to-another-row attack)...
        var swappedAssociatedData = Plaintext("oauth_tokens:access_token_cipher:google-health");
        var decryptWrongAssociatedData = () => cipher.Decrypt(envelope, swappedAssociatedData);

        // Then authentication also fails — ciphertext cannot be moved between rows.
        decryptWrongAssociatedData.Should().Throw<CryptographicException>();
    }
}
