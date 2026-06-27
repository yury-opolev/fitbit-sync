namespace FitbitSync.Providers.Fitbit;

// Single source of truth for RFC 4648 base64url WITHOUT padding (used by PKCE codes and the anti-CSRF state).
internal static class Base64UrlEncoder
{
    public static string Encode(ReadOnlySpan<byte> bytes)
        => Convert.ToBase64String(bytes)
            .Replace('+', '-')
            .Replace('/', '_')
            .TrimEnd('=');
}
