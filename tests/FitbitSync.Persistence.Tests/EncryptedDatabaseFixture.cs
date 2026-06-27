using System.Security.Cryptography;
using FitbitSync.Persistence;
using FitbitSync.Security;

namespace FitbitSync.Persistence.Tests;

// Spins up a REAL encrypted SQLite temp-file database (Phase-0 encrypted connection factory +
// EncryptedDbContextFactory), runs the schema initializer once, and exposes fresh DbContexts plus
// the Security services so integration tests exercise the true encrypted-at-rest path. The temp
// file (and its -wal/-shm siblings) are deleted on dispose.
public sealed class EncryptedDatabaseFixture : IDisposable
{
    private readonly string databasePath;

    public EncryptedDatabaseFixture()
    {
        this.databasePath = Path.Combine(Path.GetTempPath(), $"fitbitsync-it-{Guid.NewGuid():N}.db");

        var connectionFactory = new EncryptedSqliteConnectionFactory(this.databasePath, "integration-test-key");
        this.ContextFactory = new EncryptedDbContextFactory(connectionFactory);

        this.KeyProvider = new InMemoryKeyProvider(
            RandomNumberGenerator.GetBytes(32),
            RandomNumberGenerator.GetBytes(32));
        this.ColumnCipher = new AesGcmColumnCipher(this.KeyProvider.GetColumnEncryptionKey().ToArray());
        this.RecordSigner = new HmacRecordSigner(this.KeyProvider);

        using var context = this.ContextFactory.Create();
        new SchemaInitializer(context, this.KeyProvider).Initialize();
    }

    public EncryptedDbContextFactory ContextFactory { get; }

    public IKeyProvider KeyProvider { get; }

    public IColumnCipher ColumnCipher { get; }

    public IRecordSigner RecordSigner { get; }

    public FitbitSyncDbContext NewDbContext() => this.ContextFactory.Create();

    public void Dispose()
    {
        Microsoft.Data.Sqlite.SqliteConnection.ClearAllPools();

        foreach (var path in new[] { this.databasePath, this.databasePath + "-wal", this.databasePath + "-shm" })
        {
            if (File.Exists(path))
            {
                File.Delete(path);
            }
        }
    }
}
