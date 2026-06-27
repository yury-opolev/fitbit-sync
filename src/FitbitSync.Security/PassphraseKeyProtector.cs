using System.Buffers.Binary;
using System.Security.Cryptography;
using System.Text;

namespace FitbitSync.Security;

public sealed class PassphraseKeyProtector : IKeyProtector
{
    private const int SaltSize = 16;
    private const int NonceSize = 12;
    private const int TagSize = 16;
    private const int WrappingKeySize = 32;
    private const int Iterations = 600_000;
    private const byte FormatVersion = 1;
    private static readonly byte[] Magic = "FBSP"u8.ToArray();
    private static readonly int HeaderLength = Magic.Length + 1 + sizeof(int) + SaltSize + NonceSize + TagSize;

    private readonly byte[] masterSecret;

    public PassphraseKeyProtector(string masterSecret)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(masterSecret);
        this.masterSecret = Encoding.UTF8.GetBytes(masterSecret);
    }

    public byte[] Protect(ReadOnlySpan<byte> plaintext)
    {
        var salt = RandomNumberGenerator.GetBytes(SaltSize);
        var nonce = RandomNumberGenerator.GetBytes(NonceSize);
        var wrappingKey = this.DeriveWrappingKey(salt, Iterations);

        var ciphertext = new byte[plaintext.Length];
        var tag = new byte[TagSize];

        using (var aesGcm = new AesGcm(wrappingKey, TagSize))
        {
            aesGcm.Encrypt(nonce, plaintext, ciphertext, tag);
        }

        CryptographicOperations.ZeroMemory(wrappingKey);

        var blob = new byte[HeaderLength + ciphertext.Length];
        var offset = 0;

        Magic.CopyTo(blob, offset);
        offset += Magic.Length;

        blob[offset] = FormatVersion;
        offset += 1;

        BinaryPrimitives.WriteInt32BigEndian(blob.AsSpan(offset, sizeof(int)), Iterations);
        offset += sizeof(int);

        salt.CopyTo(blob, offset);
        offset += SaltSize;

        nonce.CopyTo(blob, offset);
        offset += NonceSize;

        tag.CopyTo(blob, offset);
        offset += TagSize;

        ciphertext.CopyTo(blob, offset);

        return blob;
    }

    public byte[] Unprotect(ReadOnlySpan<byte> protectedBlob)
    {
        if (protectedBlob.Length < HeaderLength)
        {
            throw new FormatException($"Protected blob must be at least {HeaderLength} bytes; got {protectedBlob.Length}.");
        }

        var offset = 0;
        if (!protectedBlob[..Magic.Length].SequenceEqual(Magic))
        {
            throw new FormatException("Protected blob has an invalid magic header.");
        }

        offset += Magic.Length;
        if (protectedBlob[offset] != FormatVersion)
        {
            throw new FormatException($"Unsupported protected blob format version {protectedBlob[offset]}.");
        }

        offset += 1;
        var iterations = BinaryPrimitives.ReadInt32BigEndian(protectedBlob.Slice(offset, sizeof(int)));
        offset += sizeof(int);

        var salt = protectedBlob.Slice(offset, SaltSize).ToArray();
        offset += SaltSize;

        var nonce = protectedBlob.Slice(offset, NonceSize);
        offset += NonceSize;

        var tag = protectedBlob.Slice(offset, TagSize);
        offset += TagSize;

        var ciphertext = protectedBlob[offset..];
        var plaintext = new byte[ciphertext.Length];
        var wrappingKey = this.DeriveWrappingKey(salt, iterations);

        try
        {
            using var aesGcm = new AesGcm(wrappingKey, TagSize);
            aesGcm.Decrypt(nonce, ciphertext, tag, plaintext);
        }
        finally
        {
            CryptographicOperations.ZeroMemory(wrappingKey);
        }

        return plaintext;
    }

    private byte[] DeriveWrappingKey(byte[] salt, int iterations) =>
        Rfc2898DeriveBytes.Pbkdf2(this.masterSecret, salt, iterations, HashAlgorithmName.SHA256, WrappingKeySize);
}
