namespace FitbitSync.Host;

public static class StorageKeyDecoder
{
    public static byte[] Decode(string base64, string name)
    {
        if (string.IsNullOrWhiteSpace(base64))
        {
            throw new InvalidOperationException($"Storage.{name} is required; supply a base64-encoded 32-byte key via User Secrets or environment variables.");
        }

        try
        {
            return Convert.FromBase64String(base64);
        }
        catch (FormatException ex)
        {
            throw new InvalidOperationException($"Storage.{name} is not valid base64.", ex);
        }
    }
}
