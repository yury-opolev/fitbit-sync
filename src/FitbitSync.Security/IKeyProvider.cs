namespace FitbitSync.Security;

public interface IKeyProvider
{
    string SigningKeyId { get; }

    ReadOnlyMemory<byte> GetColumnEncryptionKey();

    ReadOnlyMemory<byte> GetSigningKey();
}
