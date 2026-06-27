namespace FitbitSync.Persistence;

// The single pending-login row. The whole database file is encrypted at rest (SQLite3MC), so the
// short-lived, single-use code verifier needs no additional column cipher.
public sealed class PendingAuthorizationRow
{
    public int Id { get; set; }

    public string State { get; set; } = string.Empty;

    public string CodeVerifier { get; set; } = string.Empty;

    public string AuthorizeUrl { get; set; } = string.Empty;

    public DateTimeOffset ExpiresAt { get; set; }
}
