using FitbitSync.Providers.GoogleHealth;

namespace FitbitSync.Host;

public static class HostConfigurationValidator
{
    private const int RequiredKeyLength = 32;

    public static void ValidateStorage(HostStorageOptions storage)
    {
        ArgumentNullException.ThrowIfNull(storage);

        if (string.IsNullOrWhiteSpace(storage.DatabasePath))
        {
            throw new InvalidOperationException("Storage.DatabasePath is required; configure the encrypted database file path.");
        }

        if (string.IsNullOrWhiteSpace(storage.DatabasePassphrase))
        {
            throw new InvalidOperationException("Storage.DatabasePassphrase is required; supply the database passphrase via User Secrets or environment variables.");
        }

        var usesKeyFile = !string.IsNullOrWhiteSpace(storage.KeyFilePath);
        if (!usesKeyFile)
        {
            RequireKey(storage.ColumnEncryptionKeyBase64, nameof(storage.ColumnEncryptionKeyBase64));
            RequireKey(storage.SigningKeyBase64, nameof(storage.SigningKeyBase64));
        }
        else if (!OperatingSystem.IsWindows()
            && string.IsNullOrWhiteSpace(storage.KeyProtectorSecret)
            && string.IsNullOrWhiteSpace(storage.KeyProtectorSecretFile))
        {
            throw new InvalidOperationException("Storage.KeyProtectorSecret or Storage.KeyProtectorSecretFile is required to protect Storage.KeyFilePath on this platform; supply the master secret via environment variable or a mounted secret file.");
        }
    }

    public static void ValidateOAuth(GoogleOAuthOptions oauth)
    {
        ArgumentNullException.ThrowIfNull(oauth);

        if (string.IsNullOrWhiteSpace(oauth.ClientId))
        {
            throw new InvalidOperationException("Google.ClientId is required; create a Google Cloud OAuth client and supply its client id.");
        }

        if (string.IsNullOrWhiteSpace(oauth.ClientSecret))
        {
            throw new InvalidOperationException("Google.ClientSecret is required for the confidential web client; supply it via User Secrets or environment variables.");
        }

        if (oauth.RedirectUri is null || !oauth.RedirectUri.IsAbsoluteUri)
        {
            throw new InvalidOperationException("Google.RedirectUri is required and must be an absolute URI matching the OAuth client (e.g. https://localhost:7654/callback).");
        }

        if (oauth.Scopes is not { Count: > 0 })
        {
            throw new InvalidOperationException("Google.Scopes is required; configure at least one Google Health OAuth scope.");
        }
    }

    private static void RequireKey(string base64, string name)
    {
        var key = StorageKeyDecoder.Decode(base64, name);
        if (key.Length != RequiredKeyLength)
        {
            throw new InvalidOperationException($"Storage.{name} must decode to {RequiredKeyLength} bytes (256-bit); got {key.Length}.");
        }
    }
}
