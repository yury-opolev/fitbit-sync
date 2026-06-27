using FitbitSync.Providers.Fitbit;

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

    public static void ValidateOAuth(FitbitOAuthOptions oauth)
    {
        ArgumentNullException.ThrowIfNull(oauth);

        if (string.IsNullOrWhiteSpace(oauth.ClientId))
        {
            throw new InvalidOperationException("Fitbit.ClientId is required; register a Fitbit app and supply its client id.");
        }

        if (oauth.RedirectUri is null || !oauth.RedirectUri.IsAbsoluteUri)
        {
            throw new InvalidOperationException("Fitbit.RedirectUri is required and must be an absolute URI (e.g. http://127.0.0.1:7654/callback).");
        }

        if (!oauth.RedirectUri.IsLoopback)
        {
            throw new InvalidOperationException("Fitbit.RedirectUri must be a loopback address; the OAuth callback is bound to localhost only.");
        }

        if (oauth.Scopes is not { Count: > 0 })
        {
            throw new InvalidOperationException("Fitbit.Scopes is required; configure at least one OAuth scope.");
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
