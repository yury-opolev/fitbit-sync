namespace FitbitSync.Security;

public interface IColumnCipher
{
    byte[] Encrypt(ReadOnlySpan<byte> plaintext, ReadOnlySpan<byte> associatedData);

    byte[] Decrypt(ReadOnlySpan<byte> ciphertextEnvelope, ReadOnlySpan<byte> associatedData);
}
