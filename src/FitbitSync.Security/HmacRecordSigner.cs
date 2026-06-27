using System.Security.Cryptography;

namespace FitbitSync.Security;

public sealed class HmacRecordSigner : IRecordSigner
{
    private readonly IKeyProvider keyProvider;

    public HmacRecordSigner(IKeyProvider keyProvider)
    {
        ArgumentNullException.ThrowIfNull(keyProvider);
        this.keyProvider = keyProvider;
    }

    public byte[] Sign<TRecord>(TRecord record)
    {
        var canonical = CanonicalJson.ToUtf8Bytes(record);
        return HMACSHA256.HashData(this.keyProvider.GetSigningKey().Span, canonical);
    }

    public bool Verify<TRecord>(TRecord record, ReadOnlySpan<byte> signature)
    {
        var expected = this.Sign(record);
        return CryptographicOperations.FixedTimeEquals(expected, signature);
    }
}
