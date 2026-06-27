using System.Security.Cryptography;
using FitbitSync.Security;
using FluentAssertions;

namespace FitbitSync.Security.Tests;

// Phase 8: PassphraseKeyProtector is the cross-platform (Linux/container) IKeyProtector. It derives a
// wrapping key from a container-supplied master secret via PBKDF2 (random per-blob salt) and AES-GCM-wraps
// the SAME ProtectedKeyFileCodec payload DPAPI wraps on Windows. These tests prove the round-trip, that a
// flipped byte fails the GCM tag, that a wrong secret is rejected, and that the versioned blob format is
// validated — all pure BCL, no OS dependency, so the suite runs on this Windows host and on Linux alike.
public sealed class PassphraseKeyProtectorTests
{
    private const string Secret = "container-supplied-master-secret";
    private static readonly byte[] Payload = RandomNumberGenerator.GetBytes(69);

    [Fact]
    public void Protect_ThenUnprotect_RoundTripsPayload()
    {
        // Given a protector built from the master secret...
        var protector = new PassphraseKeyProtector(Secret);

        // When a payload is wrapped then unwrapped, the bytes are recovered exactly.
        var wrapped = protector.Protect(Payload);
        var recovered = protector.Unprotect(wrapped);

        recovered.Should().Equal(Payload);
    }

    [Fact]
    public void Protect_ProducesCiphertext_NotPlaintext()
    {
        var protector = new PassphraseKeyProtector(Secret);

        var wrapped = protector.Protect(Payload);

        // The wrapped blob must not contain the raw payload verbatim.
        IndexOf(wrapped, Payload).Should().Be(-1);
    }

    [Fact]
    public void Protect_UsesRandomSaltAndNonce_SoRepeatedWrapsDiffer()
    {
        var protector = new PassphraseKeyProtector(Secret);

        // Two wraps of the SAME input differ (random salt + nonce per call) yet both decode.
        var first = protector.Protect(Payload);
        var second = protector.Protect(Payload);

        first.Should().NotEqual(second);
        protector.Unprotect(first).Should().Equal(Payload);
        protector.Unprotect(second).Should().Equal(Payload);
    }

    [Fact]
    public void Unprotect_WithWrongSecret_FailsTheTag()
    {
        // Given a blob wrapped with one secret...
        var wrapped = new PassphraseKeyProtector(Secret).Protect(Payload);

        // When a protector with a DIFFERENT secret derives a wrong key, the GCM tag check fails.
        var act = () => new PassphraseKeyProtector("a-different-master-secret").Unprotect(wrapped);

        act.Should().Throw<CryptographicException>();
    }

    [Fact]
    public void Unprotect_WithFlippedCiphertextByte_FailsTheTag()
    {
        var protector = new PassphraseKeyProtector(Secret);
        var wrapped = protector.Protect(Payload);

        // Flip the final byte (inside the ciphertext); AES-GCM authentication must reject it.
        wrapped[^1] ^= 0xFF;

        var act = () => protector.Unprotect(wrapped);

        act.Should().Throw<CryptographicException>();
    }

    [Fact]
    public void Unprotect_WithFlippedSaltByte_FailsTheTag()
    {
        var protector = new PassphraseKeyProtector(Secret);
        var wrapped = protector.Protect(Payload);

        // Corrupt a salt byte (offset 9: after 4-byte magic + 1 version + 4 iterations) → wrong derived key.
        wrapped[9] ^= 0xFF;

        var act = () => protector.Unprotect(wrapped);

        act.Should().Throw<CryptographicException>();
    }

    [Fact]
    public void Unprotect_RejectsBadMagicHeader()
    {
        var protector = new PassphraseKeyProtector(Secret);
        var wrapped = protector.Protect(Payload);
        wrapped[0] ^= 0xFF;

        var act = () => protector.Unprotect(wrapped);

        act.Should().Throw<FormatException>().WithMessage("*magic*");
    }

    [Fact]
    public void Unprotect_RejectsUnsupportedVersion()
    {
        var protector = new PassphraseKeyProtector(Secret);
        var wrapped = protector.Protect(Payload);
        wrapped[4] = 0x7F;

        var act = () => protector.Unprotect(wrapped);

        act.Should().Throw<FormatException>().WithMessage("*version*");
    }

    [Fact]
    public void Unprotect_RejectsTruncatedBlob()
    {
        var protector = new PassphraseKeyProtector(Secret);
        var wrapped = protector.Protect(Payload);

        var act = () => protector.Unprotect(wrapped.AsSpan(0, 10).ToArray());

        act.Should().Throw<FormatException>();
    }

    [Fact]
    public void Constructor_RejectsEmptyMasterSecret()
    {
        var act = () => new PassphraseKeyProtector("   ");

        act.Should().Throw<ArgumentException>();
    }

    private static int IndexOf(byte[] haystack, byte[] needle)
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
                return i;
            }
        }

        return -1;
    }
}
