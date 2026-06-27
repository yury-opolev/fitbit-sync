using System.Security.Cryptography;

namespace FitbitSync.Providers.Fitbit;

// Default CSPRNG-backed implementation.
internal sealed class CryptoRandomBytesGenerator : IRandomBytesGenerator
{
    public void Fill(Span<byte> buffer) => RandomNumberGenerator.Fill(buffer);
}
