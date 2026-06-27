using FitbitSync.Domain;
using FitbitSync.Persistence;
using FluentAssertions;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace FitbitSync.Persistence.Tests;

// Phase 2 security properties for the encrypted schema: the at-rest file is unreadable without the
// correct key, SchemaInitializer records the encryption-scheme version and signing-key id in
// schema_metadata, and RowVersion concurrency tokens make stale writes fail. Each operation uses a
// FRESH DbContext; the standalone wrong-key database is cleaned up alongside the fixture.
public sealed class EncryptedSchemaTests : IDisposable
{
    private readonly EncryptedDatabaseFixture fixture = new();
    private readonly List<string> extraDatabasePaths = [];

    private static readonly DateTimeOffset SeedTimestamp = new(2024, 5, 1, 6, 0, 0, TimeSpan.Zero);

    [Fact]
    public void EncryptedSchema_WrongKeyOrNoKey_FailsToRead()
    {
        // Given a freshly initialized encrypted database written under the correct key...
        var path = Path.Combine(Path.GetTempPath(), $"fitbitsync-schema-{Guid.NewGuid():N}.db");
        this.extraDatabasePaths.Add(path);

        var factory = new EncryptedSqliteConnectionFactory(path, "the-real-key");
        using (var context = new EncryptedDbContextFactory(factory).Create())
        {
            new SchemaInitializer(context, this.fixture.KeyProvider).Initialize();
        }

        // When reopened with the WRONG key, reading the encrypted bytes fails...
        var openWithWrongKey = () =>
        {
            using var connection = factory.CreateOpenConnectionWithKey("the-wrong-key");
            ReadSchemaMetadataCount(connection);
        };
        openWithWrongKey.Should().Throw<SqliteException>();

        // ...and with NO key it likewise fails — proof the file is encrypted at rest.
        var openWithoutKey = () =>
        {
            using var connection = factory.CreateOpenConnectionWithoutKey();
            ReadSchemaMetadataCount(connection);
        };
        openWithoutKey.Should().Throw<SqliteException>();
    }

    [Fact]
    public void SchemaMetadata_RecordsEncryptionSchemeVersionAndSigningKeyId()
    {
        // The fixture already ran SchemaInitializer; the metadata rows must be present and correct.
        using var context = this.fixture.NewDbContext();
        var metadata = context.SchemaMetadata.ToDictionary(row => row.Key, row => row.Value);

        metadata.Should().ContainKey("encryption_scheme_version")
            .WhoseValue.Should().Be(SchemaInitializer.EncryptionSchemeVersion);
        metadata.Should().ContainKey("signing_key_id")
            .WhoseValue.Should().Be(this.fixture.KeyProvider.SigningKeyId);
    }

    [Fact]
    public async Task SyncCheckpoint_StaleRowVersion_ThrowsConcurrency()
    {
        // Given a persisted checkpoint...
        using (var seed = this.fixture.NewDbContext())
        {
            seed.SyncCheckpoints.Add(new SyncCheckpointRow
            {
                Metric = MetricType.HeartRate,
                NewestSynced = SeedTimestamp,
                RowVersion = Guid.NewGuid(),
            });
            await seed.SaveChangesAsync();
        }

        // ...loaded independently by two contexts (both holding the same original RowVersion)...
        using var contextA = this.fixture.NewDbContext();
        using var contextB = this.fixture.NewDbContext();

        var rowA = await contextA.SyncCheckpoints.SingleAsync(row => row.Metric == MetricType.HeartRate);
        var rowB = await contextB.SyncCheckpoints.SingleAsync(row => row.Metric == MetricType.HeartRate);

        // When the first writer commits, it bumps the RowVersion token...
        rowA.NewestSynced = SeedTimestamp.AddHours(1);
        rowA.RowVersion = Guid.NewGuid();
        await contextA.SaveChangesAsync();

        // ...then the second writer's stale token matches no row and the save is rejected.
        rowB.NewestSynced = SeedTimestamp.AddHours(2);
        rowB.RowVersion = Guid.NewGuid();
        var staleSave = async () => await contextB.SaveChangesAsync();
        await staleSave.Should().ThrowAsync<DbUpdateConcurrencyException>();
    }

    private static void ReadSchemaMetadataCount(SqliteConnection connection)
    {
        using var command = connection.CreateCommand();
        command.CommandText = "SELECT COUNT(*) FROM schema_metadata;";
        command.ExecuteScalar();
    }

    public void Dispose()
    {
        this.fixture.Dispose();

        SqliteConnection.ClearAllPools();
        foreach (var basePath in this.extraDatabasePaths)
        {
            foreach (var path in new[] { basePath, basePath + "-wal", basePath + "-shm" })
            {
                if (File.Exists(path))
                {
                    File.Delete(path);
                }
            }
        }
    }
}
