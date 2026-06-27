namespace FitbitSync.Providers.Fitbit;

// An RFC 7636 PKCE pair: the secret verifier and its derived S256 challenge.
internal sealed record PkceCodes(string Verifier, string Challenge);
