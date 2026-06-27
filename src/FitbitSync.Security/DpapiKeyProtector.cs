using System.Runtime.Versioning;
using System.Security.Cryptography;

namespace FitbitSync.Security;

[SupportedOSPlatform("windows")]
public sealed class DpapiKeyProtector : IKeyProtector
{
    private static readonly byte[] Entropy = "FitbitSync.KeyProtector.v1"u8.ToArray();

    public byte[] Protect(ReadOnlySpan<byte> plaintext) =>
        ProtectedData.Protect(plaintext.ToArray(), Entropy, DataProtectionScope.CurrentUser);

    public byte[] Unprotect(ReadOnlySpan<byte> protectedBlob) =>
        ProtectedData.Unprotect(protectedBlob.ToArray(), Entropy, DataProtectionScope.CurrentUser);
}
