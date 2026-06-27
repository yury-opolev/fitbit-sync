using System.Security.Cryptography;
using FitbitSync.Domain;
using FitbitSync.Persistence;
using FitbitSync.Security;
using FluentAssertions;
using Microsoft.Data.Sqlite;

namespace FitbitSync.Persistence.Tests;

// Phase 7 key rotation: KeyRotationService re-signs every metric_samples row under a NEW signing key
// and re-encrypts the whole database file with a NEW passphrase (PRAGMA rekey), atomically and with an
// audit entry. This test owns its temp file and both passphrases because rotation changes the passphrase
// mid-test (the shared EncryptedDatabaseFixture uses one fixed key).
public sealed class KeyRotationServiceTests : IDisposable
{
    private const string OldPassphrase = "old-correct-horse";
    private const string NewPassphrase = "new-battery-staple";

    private static readonly DateTimeOffset SampleTimestamp = new(2024, 5, 1, 10, 0, 0, TimeSpan.Zero);

    private readonly string databasePath;
    private readonly AuditEntryHasher hasher = new();
    private readonly MutableClock clock = new();
    private readonly IKeyProvider oldKeyProvider;

    public KeyRotationServiceTests()
    {
        this.databasePath = Path.Combine(Path.GetTempPath(), $"fitbitsync-rotate-{Guid.NewGuid():N}.db");
        this.oldKeyProvider = new InMemoryKeyProvider(RandomNumberGenerator.GetBytes(32), RandomNumberGenerator.GetBytes(32));

        var factory = new EncryptedDbContextFactory(new EncryptedSqliteConnectionFactory(this.databasePath, OldPassphrase));
        using var context = factory.Create();
        new SchemaInitializer(context, this.oldKeyProvider).Initialize();

        var repository = new MetricRepository(context, new HmacRecordSigner(this.oldKeyProvider), this.oldKeyProvider);
        repository.UpsertAsync([new MetricSample(MetricType.HeartRate, SampleTimestamp, 72, "bpm", IntradayResolution.OneMinute, "fitbit")]).GetAwaiter().GetResult();
    }

    [Fact]
    public async Task RotateKeys_ResignsSamples_RekeysDatabase_AndKeepsIntegrityValid()
    {
        var newKeyProvider = new InMemoryKeyProvider(RandomNumberGenerator.GetBytes(32), RandomNumberGenerator.GetBytes(32));

        // When rotation runs over a context opened with the OLD passphrase...
        var oldFactory = new EncryptedSqliteConnectionFactory(this.databasePath, OldPassphrase);
        KeyRotationResult result;
        using (var context = new EncryptedDbContextFactory(oldFactory).Create())
        {
            var auditTrail = new AuditTrail(context, this.clock, this.hasher);
            var service = new KeyRotationService(context, oldFactory, auditTrail);
            result = await service.RotateAsync(newKeyProvider, NewPassphrase);
        }

        SqliteConnection.ClearAllPools();

        // Then the result reports the re-sign + rekey under the new signing key id.
        result.ResignedSampleCount.Should().Be(1);
        result.NewSigningKeyId.Should().Be(newKeyProvider.SigningKeyId);
        result.DatabaseRekeyed.Should().BeTrue();

        // And the OLD passphrase can no longer open the re-encrypted file.
        var openWithOldPassphrase = () =>
        {
            using var connection = new EncryptedSqliteConnectionFactory(this.databasePath, OldPassphrase).CreateOpenConnection();
            using var read = connection.CreateCommand();
            read.CommandText = "SELECT COUNT(*) FROM metric_samples;";
            read.ExecuteScalar();
        };
        openWithOldPassphrase.Should().Throw<SqliteException>();

        // And under the NEW passphrase + NEW signing key, integrity verifies and rows carry the new key id.
        var newFactory = new EncryptedDbContextFactory(new EncryptedSqliteConnectionFactory(this.databasePath, NewPassphrase));
        using var verifyContext = newFactory.Create();
        var verifier = new IntegrityVerifier(verifyContext, new AuditTrail(verifyContext, this.clock, this.hasher), new HmacRecordSigner(newKeyProvider));
        var report = await verifier.VerifyAsync();

        report.IsValid.Should().BeTrue();
        report.VerifiedSampleCount.Should().Be(1);
        verifyContext.MetricSamples.Single().SignatureKeyId.Should().Be(newKeyProvider.SigningKeyId);
    }

    public void Dispose()
    {
        SqliteConnection.ClearAllPools();

        foreach (var path in new[] { this.databasePath, this.databasePath + "-wal", this.databasePath + "-shm" })
        {
            if (File.Exists(path))
            {
                File.Delete(path);
            }
        }
    }

    private sealed class MutableClock : IClock
    {
        public DateTimeOffset UtcNow { get; set; } = DateTimeOffset.UnixEpoch;
    }
}
