using System.Security.Cryptography;
using FitbitSync.Security;
using FluentAssertions;

namespace FitbitSync.Security.Tests;

// Phase 1 / Increment 3: every stored record is signed (HMAC-SHA256 over canonical JSON) so
// out-of-band tampering is detectable. Canonicalization guarantees logically-equal records
// produce identical signed bytes regardless of property order or culture.
public sealed class HmacRecordSignerTests
{
    private sealed record SampleRecord(string Provider, string MetricType, int Value, string EffectiveDate);

    private static IRecordSigner CreateSigner(out byte[] signingKey)
    {
        signingKey = RandomNumberGenerator.GetBytes(32);
        var provider = new InMemoryKeyProvider(RandomNumberGenerator.GetBytes(32), signingKey);
        return new HmacRecordSigner(provider);
    }

    [Fact]
    public void Sign_ThenVerify_ReturnsTrue_ForUntamperedRecord()
    {
        // Given a signed record...
        var signer = CreateSigner(out _);
        var record = new SampleRecord("fitbit", "HeartRate", 72, "2024-05-01");

        // When verifying the original record against its signature, it succeeds.
        var signature = signer.Sign(record);
        signer.Verify(record, signature).Should().BeTrue();
    }

    [Fact]
    public void Verify_Fails_WhenAnyFieldIsMutated()
    {
        // Given a record signed in its original state...
        var signer = CreateSigner(out _);
        var record = new SampleRecord("fitbit", "HeartRate", 72, "2024-05-01");
        var signature = signer.Sign(record);

        // When a single field is mutated, the signature no longer verifies.
        var mutated = record with { Value = 73 };
        signer.Verify(mutated, signature).Should().BeFalse();
    }

    [Fact]
    public void Sign_IsStable_RegardlessOfPropertyOrder()
    {
        // Given two anonymous records with the SAME data but different property declaration order...
        var signer = CreateSigner(out _);
        var ordered = new { Provider = "fitbit", Value = 72 };
        var reordered = new { Value = 72, Provider = "fitbit" };

        // Then canonicalization yields identical signatures (order does not matter).
        signer.Sign(ordered).Should().Equal(signer.Sign(reordered));
    }

    [Fact]
    public void Verify_Fails_WithDifferentSigningKey()
    {
        // Given a record signed with one key...
        var signer = CreateSigner(out _);
        var record = new SampleRecord("fitbit", "HeartRate", 72, "2024-05-01");
        var signature = signer.Sign(record);

        // When verified by a signer holding a DIFFERENT signing key, it fails (forgery rejected).
        var otherSigner = CreateSigner(out _);
        otherSigner.Verify(record, signature).Should().BeFalse();
    }
}
