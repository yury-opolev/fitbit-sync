using System.Security.Cryptography;
using FluentAssertions;

namespace FitbitSync.Host.Tests;

// StorageKeyDecoder is the single base64 key-decoding seam shared by the composition root and the
// rotate-keys command. It must decode valid base64 and fail fast with a named, actionable error on
// missing or malformed input.
public sealed class StorageKeyDecoderTests
{
    [Fact]
    public void Decode_ValidBase64_ReturnsBytes()
    {
        var raw = RandomNumberGenerator.GetBytes(32);

        var decoded = StorageKeyDecoder.Decode(Convert.ToBase64String(raw), "SigningKeyBase64");

        decoded.Should().Equal(raw);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void Decode_MissingValue_ThrowsNamedError(string value)
    {
        var act = () => StorageKeyDecoder.Decode(value, "SigningKeyBase64");

        act.Should().Throw<InvalidOperationException>()
            .WithMessage("*SigningKeyBase64*required*");
    }

    [Fact]
    public void Decode_InvalidBase64_ThrowsNamedError()
    {
        var act = () => StorageKeyDecoder.Decode("not-base64!!!", "ColumnEncryptionKeyBase64");

        act.Should().Throw<InvalidOperationException>()
            .WithMessage("*ColumnEncryptionKeyBase64*not valid base64*");
    }
}
