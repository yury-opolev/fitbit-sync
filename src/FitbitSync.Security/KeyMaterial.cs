namespace FitbitSync.Security;

public sealed record KeyMaterial(byte[] ColumnEncryptionKey, byte[] SigningKey);
