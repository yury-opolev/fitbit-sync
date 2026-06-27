namespace FitbitSync.Host;

public static class KeyProtectorSecretResolver
{
    public static string Resolve(string secret, string secretFilePath)
    {
        if (!string.IsNullOrWhiteSpace(secret))
        {
            return secret.Trim();
        }

        if (!string.IsNullOrWhiteSpace(secretFilePath))
        {
            if (!File.Exists(secretFilePath))
            {
                throw new InvalidOperationException($"Storage.KeyProtectorSecretFile '{secretFilePath}' does not exist; mount the secret file or set Storage.KeyProtectorSecret.");
            }

            var contents = File.ReadAllText(secretFilePath).Trim();
            if (string.IsNullOrWhiteSpace(contents))
            {
                throw new InvalidOperationException($"Storage.KeyProtectorSecretFile '{secretFilePath}' is empty; the key-protection master secret must be non-empty.");
            }

            return contents;
        }

        throw new InvalidOperationException("Storage.KeyProtectorSecret or Storage.KeyProtectorSecretFile is required to protect the key file on this platform; supply the master secret via environment variable or a mounted secret file.");
    }
}
