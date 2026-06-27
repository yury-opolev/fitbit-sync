using System.Security.Cryptography;
using FitbitSync.Security;
using FluentAssertions;

namespace FitbitSync.Security.Tests;

// Phase 7: the protected-key-file codec serializes the two 256-bit keys (column + signing) into a
// versioned, self-describing payload that an IKeyProtector then wraps. These tests prove the codec
// round-trips and rejects malformed payloads WITHOUT any real DPAPI dependency — DPAPI sits behind the
// IKeyProtector seam and is exercised here through an in-memory fake, so the suite builds and runs on
// any OS.
public sealed class ProtectedKeyFileCodecTests
{
    private static readonly byte[] ColumnKey = RandomNumberGenerator.GetBytes(32);
    private static readonly byte[] SigningKey = RandomNumberGenerator.GetBytes(32);

    [Fact]
    public void Serialize_ThenDeserialize_RoundTripsBothKeys()
    {
        // Given key material serialized to the protected-file payload...
        var payload = ProtectedKeyFileCodec.Serialize(new KeyMaterial(ColumnKey, SigningKey));

        // When deserialized, both keys are recovered byte-for-byte.
        var material = ProtectedKeyFileCodec.Deserialize(payload);

        material.ColumnEncryptionKey.Should().Equal(ColumnKey);
        material.SigningKey.Should().Equal(SigningKey);
    }

    [Fact]
    public void Serialize_ThenWrapUnwrap_ThroughFakeProtector_RoundTrips()
    {
        // Given the codec composed with a fake protector (stands in for DPAPI, no OS dependency)...
        IKeyProtector protector = new XorKeyProtector();
        var payload = ProtectedKeyFileCodec.Serialize(new KeyMaterial(ColumnKey, SigningKey));

        // When wrapped then unwrapped, the protected bytes differ from plaintext but decode cleanly.
        var wrapped = protector.Protect(payload);
        wrapped.Should().NotEqual(payload);

        var material = ProtectedKeyFileCodec.Deserialize(protector.Unprotect(wrapped));

        material.ColumnEncryptionKey.Should().Equal(ColumnKey);
        material.SigningKey.Should().Equal(SigningKey);
    }

    [Fact]
    public void Deserialize_RejectsTruncatedPayload()
    {
        var payload = ProtectedKeyFileCodec.Serialize(new KeyMaterial(ColumnKey, SigningKey));

        var act = () => ProtectedKeyFileCodec.Deserialize(payload.AsSpan(0, payload.Length - 1).ToArray());

        act.Should().Throw<FormatException>();
    }

    [Fact]
    public void Deserialize_RejectsBadMagicHeader()
    {
        var payload = ProtectedKeyFileCodec.Serialize(new KeyMaterial(ColumnKey, SigningKey));
        payload[0] ^= 0xFF;

        var act = () => ProtectedKeyFileCodec.Deserialize(payload);

        act.Should().Throw<FormatException>().WithMessage("*magic*");
    }

    [Fact]
    public void Serialize_RejectsWrongLengthKey()
    {
        var act = () => ProtectedKeyFileCodec.Serialize(new KeyMaterial(new byte[16], SigningKey));

        act.Should().Throw<ArgumentException>();
    }

    private sealed class XorKeyProtector : IKeyProtector
    {
        private const byte Mask = 0x5A;

        public byte[] Protect(ReadOnlySpan<byte> plaintext) => Transform(plaintext);

        public byte[] Unprotect(ReadOnlySpan<byte> protectedBlob) => Transform(protectedBlob);

        private static byte[] Transform(ReadOnlySpan<byte> input)
        {
            var output = input.ToArray();
            for (var i = 0; i < output.Length; i++)
            {
                output[i] ^= Mask;
            }

            return output;
        }
    }
}
