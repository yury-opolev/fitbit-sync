namespace FitbitSync.Security;

public interface IRecordSigner
{
    byte[] Sign<TRecord>(TRecord record);

    bool Verify<TRecord>(TRecord record, ReadOnlySpan<byte> signature);
}
