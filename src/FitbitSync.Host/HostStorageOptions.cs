namespace FitbitSync.Host;

public sealed class HostStorageOptions
{
    public const string SectionName = "Storage";

    public string DatabasePath { get; set; } = "";

    public string DatabasePassphrase { get; set; } = "";

    public string ColumnEncryptionKeyBase64 { get; set; } = "";

    public string SigningKeyBase64 { get; set; } = "";

    public string KeyFilePath { get; set; } = "";

    public string KeyProtectorSecret { get; set; } = "";

    public string KeyProtectorSecretFile { get; set; } = "";

    public string NewColumnEncryptionKeyBase64 { get; set; } = "";

    public string NewSigningKeyBase64 { get; set; } = "";

    public string NewDatabasePassphrase { get; set; } = "";
}
