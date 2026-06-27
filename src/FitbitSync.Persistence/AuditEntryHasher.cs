using System.Security.Cryptography;
using FitbitSync.Security;

namespace FitbitSync.Persistence;

public sealed class AuditEntryHasher
{
    public string ComputeHash(long sequence, DateTimeOffset timestamp, string action, string prevHash)
    {
        var canonical = CanonicalJson.ToUtf8Bytes(new
        {
            sequence,
            timestamp,
            action,
            prevHash,
        });

        var buffer = new byte[canonical.Length + prevHash.Length];
        canonical.CopyTo(buffer, 0);
        System.Text.Encoding.UTF8.GetBytes(prevHash, 0, prevHash.Length, buffer, canonical.Length);

        return Convert.ToHexStringLower(SHA256.HashData(buffer));
    }
}
