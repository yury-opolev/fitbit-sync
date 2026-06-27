namespace FitbitSync.Security;

public sealed class InMemoryKeyProvider : IKeyProvider
{
    private const int RequiredKeyLength = 32;

    private readonly byte[] columnEncryptionKey;
    private readonly byte[] signingKey;
    private readonly string signingKeyId;

    public InMemoryKeyProvider(ReadOnlySpan<byte> columnEncryptionKey, ReadOnlySpan<byte> signingKey)
    {
        this.columnEncryptionKey = CopyValidated(columnEncryptionKey, nameof(columnEncryptionKey));
        this.signingKey = CopyValidated(signingKey, nameof(signingKey));
        this.signingKeyId = DeriveKeyId(this.signingKey);
    }

    public string SigningKeyId => this.signingKeyId;

    public ReadOnlyMemory<byte> GetColumnEncryptionKey() => this.columnEncryptionKey;

    public ReadOnlyMemory<byte> GetSigningKey() => this.signingKey;

    private static string DeriveKeyId(byte[] key)
    {
        var fingerprint = System.Security.Cryptography.SHA256.HashData(key);
        return Convert.ToHexStringLower(fingerprint.AsSpan(0, 8));
    }

    private static byte[] CopyValidated(ReadOnlySpan<byte> key, string parameterName)
    {
        if (key.Length != RequiredKeyLength)
        {
            throw new ArgumentException($"Key must be {RequiredKeyLength} bytes (256-bit).", parameterName);
        }

        return key.ToArray();
    }
}
