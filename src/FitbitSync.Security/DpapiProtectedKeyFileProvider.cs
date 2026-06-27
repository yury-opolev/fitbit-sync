using System.Security.Cryptography;

namespace FitbitSync.Security;

public sealed class DpapiProtectedKeyFileProvider : IKeyProvider
{
    private const int KeyLength = 32;

    private readonly InMemoryKeyProvider inner;

    public DpapiProtectedKeyFileProvider(string keyFilePath, IKeyProtector protector)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(keyFilePath);
        ArgumentNullException.ThrowIfNull(protector);

        var material = LoadOrCreate(keyFilePath, protector);
        this.inner = new InMemoryKeyProvider(material.ColumnEncryptionKey, material.SigningKey);
    }

    public string SigningKeyId => this.inner.SigningKeyId;

    public ReadOnlyMemory<byte> GetColumnEncryptionKey() => this.inner.GetColumnEncryptionKey();

    public ReadOnlyMemory<byte> GetSigningKey() => this.inner.GetSigningKey();

    private static KeyMaterial LoadOrCreate(string keyFilePath, IKeyProtector protector)
    {
        if (File.Exists(keyFilePath))
        {
            var wrapped = File.ReadAllBytes(keyFilePath);
            return ProtectedKeyFileCodec.Deserialize(protector.Unprotect(wrapped));
        }

        var material = new KeyMaterial(
            RandomNumberGenerator.GetBytes(KeyLength),
            RandomNumberGenerator.GetBytes(KeyLength));

        var directory = Path.GetDirectoryName(keyFilePath);
        if (!string.IsNullOrEmpty(directory))
        {
            Directory.CreateDirectory(directory);
        }

        var protectedBlob = protector.Protect(ProtectedKeyFileCodec.Serialize(material));
        File.WriteAllBytes(keyFilePath, protectedBlob);
        RestrictToOwner(keyFilePath);

        return material;
    }

    private static void RestrictToOwner(string keyFilePath)
    {
        if (!OperatingSystem.IsWindows())
        {
            File.SetUnixFileMode(keyFilePath, UnixFileMode.UserRead | UnixFileMode.UserWrite);
        }
    }
}
