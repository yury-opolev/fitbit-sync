using System.Security.Cryptography;
using FitbitSync.Security;
using FluentAssertions;

namespace FitbitSync.Security.Tests;

// Phase 7: the protected-key-file IKeyProvider creates a wrapped key file on first use and reloads the
// SAME key material on subsequent constructions, so the encrypted database stays readable across runs.
// The actual DPAPI wrap sits behind IKeyProtector; here a fake protector stands in so the load/create/
// reload logic is unit-tested on any OS without a real DPAPI call.
public sealed class DpapiProtectedKeyFileProviderTests : IDisposable
{
    private readonly string keyFilePath;
    private readonly IKeyProtector protector = new XorKeyProtector();

    public DpapiProtectedKeyFileProviderTests()
    {
        this.keyFilePath = Path.Combine(Path.GetTempPath(), $"fitbitsync-keyfile-{Guid.NewGuid():N}.bin");
    }

    [Fact]
    public void Provider_CreatesKeyFile_OnFirstUse_WithValidLengthKeys()
    {
        // Given no key file yet, when the provider is constructed...
        var provider = new DpapiProtectedKeyFileProvider(this.keyFilePath, this.protector);

        // Then a wrapped key file exists and the provider yields 256-bit keys plus a signing-key id.
        File.Exists(this.keyFilePath).Should().BeTrue();
        provider.GetColumnEncryptionKey().Length.Should().Be(32);
        provider.GetSigningKey().Length.Should().Be(32);
        provider.SigningKeyId.Should().NotBeNullOrEmpty();
    }

    [Fact]
    public void Provider_ReloadsSameKeys_OnSubsequentConstruction()
    {
        // Given a provider that created the key file...
        var first = new DpapiProtectedKeyFileProvider(this.keyFilePath, this.protector);

        // When a second provider opens the existing file...
        var second = new DpapiProtectedKeyFileProvider(this.keyFilePath, this.protector);

        // Then it recovers identical key material (so the encrypted DB remains readable across runs).
        second.GetColumnEncryptionKey().ToArray().Should().Equal(first.GetColumnEncryptionKey().ToArray());
        second.GetSigningKey().ToArray().Should().Equal(first.GetSigningKey().ToArray());
        second.SigningKeyId.Should().Be(first.SigningKeyId);
    }

    [Fact]
    public void Provider_WritesWrappedBytes_NotRawKeyMaterial()
    {
        // Given a created key file...
        var provider = new DpapiProtectedKeyFileProvider(this.keyFilePath, this.protector);

        // Then the raw signing key must NOT appear verbatim in the (wrapped) file bytes.
        var fileBytes = File.ReadAllBytes(this.keyFilePath);
        IndexOf(fileBytes, provider.GetSigningKey().ToArray()).Should().Be(-1);
    }

    public void Dispose()
    {
        if (File.Exists(this.keyFilePath))
        {
            File.Delete(this.keyFilePath);
        }
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

    private sealed class XorKeyProtector : IKeyProtector
    {
        private const byte Mask = 0x5A;

        public byte[] Protect(ReadOnlySpan<byte> plaintext) => Transform(plaintext);

        public byte[] Unprotect(ReadOnlySpan<byte> protectedBlob) => Transform(protectedBlob);

        private static byte[] Transform(ReadOnlySpan<byte> input)
        {
            var output = input.ToArray();
            for (var i = 0; i < output.Length; i++)
            {
                output[i] ^= Mask;
            }

            return output;
        }
    }
}
