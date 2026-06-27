using System.Security.Cryptography;

namespace FitbitSync.Security;

public sealed class AesGcmColumnCipher : IColumnCipher
{
    private const int NonceSize = 12;
    private const int TagSize = 16;

    private readonly byte[] key;

    public AesGcmColumnCipher(byte[] key)
    {
        ArgumentNullException.ThrowIfNull(key);
        if (key.Length is not (16 or 24 or 32))
        {
            throw new ArgumentException("AES key must be 128, 192, or 256 bits.", nameof(key));
        }

        this.key = (byte[])key.Clone();
    }

    public byte[] Encrypt(ReadOnlySpan<byte> plaintext, ReadOnlySpan<byte> associatedData)
    {
        Span<byte> nonce = stackalloc byte[NonceSize];
        RandomNumberGenerator.Fill(nonce);

        var envelope = new byte[NonceSize + TagSize + plaintext.Length];
        var nonceSegment = envelope.AsSpan(0, NonceSize);
        var tagSegment = envelope.AsSpan(NonceSize, TagSize);
        var ciphertextSegment = envelope.AsSpan(NonceSize + TagSize);

        nonce.CopyTo(nonceSegment);

        using var aesGcm = new AesGcm(this.key, TagSize);
        aesGcm.Encrypt(nonce, plaintext, ciphertextSegment, tagSegment, associatedData);

        return envelope;
    }

    public byte[] Decrypt(ReadOnlySpan<byte> ciphertextEnvelope, ReadOnlySpan<byte> associatedData)
    {
        if (ciphertextEnvelope.Length < NonceSize + TagSize)
        {
            throw new ArgumentException("Ciphertext envelope is too short.", nameof(ciphertextEnvelope));
        }

        var nonce = ciphertextEnvelope[..NonceSize];
        var tag = ciphertextEnvelope.Slice(NonceSize, TagSize);
        var ciphertext = ciphertextEnvelope[(NonceSize + TagSize)..];

        var plaintext = new byte[ciphertext.Length];

        using var aesGcm = new AesGcm(this.key, TagSize);
        aesGcm.Decrypt(nonce, ciphertext, tag, plaintext, associatedData);

        return plaintext;
    }
}
