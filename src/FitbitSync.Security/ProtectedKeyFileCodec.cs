namespace FitbitSync.Security;

public static class ProtectedKeyFileCodec
{
    private const int KeyLength = 32;
    private static readonly byte[] Magic = "FBSK"u8.ToArray();
    private const byte FormatVersion = 1;

    public static byte[] Serialize(KeyMaterial material)
    {
        ArgumentNullException.ThrowIfNull(material);
        ValidateKeyLength(material.ColumnEncryptionKey, nameof(material.ColumnEncryptionKey));
        ValidateKeyLength(material.SigningKey, nameof(material.SigningKey));

        var buffer = new byte[Magic.Length + 1 + KeyLength + KeyLength];
        var offset = 0;

        Magic.CopyTo(buffer, offset);
        offset += Magic.Length;

        buffer[offset] = FormatVersion;
        offset += 1;

        material.ColumnEncryptionKey.CopyTo(buffer, offset);
        offset += KeyLength;

        material.SigningKey.CopyTo(buffer, offset);

        return buffer;
    }

    public static KeyMaterial Deserialize(ReadOnlySpan<byte> payload)
    {
        var expectedLength = Magic.Length + 1 + KeyLength + KeyLength;
        if (payload.Length != expectedLength)
        {
            throw new FormatException($"Protected key payload must be {expectedLength} bytes; got {payload.Length}.");
        }

        if (!payload[..Magic.Length].SequenceEqual(Magic))
        {
            throw new FormatException("Protected key payload has an invalid magic header.");
        }

        var offset = Magic.Length;
        if (payload[offset] != FormatVersion)
        {
            throw new FormatException($"Unsupported protected key format version {payload[offset]}.");
        }

        offset += 1;
        var columnKey = payload.Slice(offset, KeyLength).ToArray();
        offset += KeyLength;
        var signingKey = payload.Slice(offset, KeyLength).ToArray();

        return new KeyMaterial(columnKey, signingKey);
    }

    private static void ValidateKeyLength(byte[] key, string name)
    {
        ArgumentNullException.ThrowIfNull(key, name);
        if (key.Length != KeyLength)
        {
            throw new ArgumentException($"Key must be {KeyLength} bytes (256-bit).", name);
        }
    }
}
