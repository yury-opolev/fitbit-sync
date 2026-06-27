namespace FitbitSync.Providers.Fitbit;

// CSPRNG seam (internal): lets tests inject deterministic bytes for PKCE verifier generation.
internal interface IRandomBytesGenerator
{
    void Fill(Span<byte> buffer);
}
