using System.Security.Cryptography;
using System.Text;

namespace FitbitSync.Providers.Fitbit;

internal sealed class PkceGenerator
{
    private readonly IRandomBytesGenerator randomBytes;

    public PkceGenerator(IRandomBytesGenerator randomBytes)
    {
        ArgumentNullException.ThrowIfNull(randomBytes);
        this.randomBytes = randomBytes;
    }

    public PkceCodes Generate()
    {
        Span<byte> seed = stackalloc byte[32];
        this.randomBytes.Fill(seed);

        var verifier = Base64UrlEncoder.Encode(seed);
        var challenge = Base64UrlEncoder.Encode(SHA256.HashData(Encoding.ASCII.GetBytes(verifier)));

        return new PkceCodes(verifier, challenge);
    }
}
