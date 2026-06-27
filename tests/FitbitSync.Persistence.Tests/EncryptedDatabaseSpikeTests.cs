using FitbitSync.Persistence;
using FluentAssertions;
using Microsoft.Data.Sqlite;

namespace FitbitSync.Persistence.Tests;

// Phase 0 encryption spike: proves SQLite3MC whole-file encryption works on .NET 10
// (the `Password` connection-string keyword caveat) before anything depends on it.
public sealed class EncryptedDatabaseSpikeTests : IDisposable
{
    private readonly string databasePath;

    public EncryptedDatabaseSpikeTests()
    {
        // Each test gets an isolated temp database file so runs don't collide.
        this.databasePath = Path.Combine(Path.GetTempPath(), $"fitbitsync-spike-{Guid.NewGuid():N}.db");
    }

    [Fact]
    public void EncryptedDatabase_RoundTripsValue_WithCorrectKey()
    {
        // Given an encrypted database created with a key, written through one connection...
        var factory = new EncryptedSqliteConnectionFactory(this.databasePath, "correct horse battery staple");
        SeedSecret(factory, "token-material");

        // When reopened with the same key, the value is readable.
        using var connection = factory.CreateOpenConnection();
        ReadSecret(connection).Should().Be("token-material");
    }

    [Fact]
    public void EncryptedDatabase_CannotBeOpenedWithoutKey()
    {
        // Given an encrypted database holding a secret...
        var factory = new EncryptedSqliteConnectionFactory(this.databasePath, "correct horse battery staple");
        SeedSecret(factory, "token-material");

        // When the file is opened with NO key, reading must fail — proof the bytes are encrypted at rest.
        var openWithoutKey = () =>
        {
            using var connection = factory.CreateOpenConnectionWithoutKey();
            ReadSecret(connection);
        };

        openWithoutKey.Should().Throw<SqliteException>();
    }

    [Fact]
    public void EncryptedDatabase_CannotBeOpenedWithWrongKey()
    {
        // Given an encrypted database holding a secret...
        var factory = new EncryptedSqliteConnectionFactory(this.databasePath, "correct horse battery staple");
        SeedSecret(factory, "token-material");

        // When opened with the WRONG key, reading must fail.
        var openWithWrongKey = () =>
        {
            using var connection = factory.CreateOpenConnectionWithKey("the wrong key");
            ReadSecret(connection);
        };

        openWithWrongKey.Should().Throw<SqliteException>();
    }

    [Fact]
    public void EncryptedDatabase_RawFileBytes_DoNotContainPlaintext()
    {
        // Given a secret written into the encrypted database...
        var factory = new EncryptedSqliteConnectionFactory(this.databasePath, "correct horse battery staple");
        SeedSecret(factory, "PLAINTEXT-NEEDLE-1234567890");

        // When scanning the raw file bytes, the plaintext needle must NOT appear.
        var bytes = File.ReadAllBytes(this.databasePath);
        var needle = "PLAINTEXT-NEEDLE-1234567890"u8.ToArray();

        IndexOf(bytes, needle).Should().Be(-1);
    }

    private static void SeedSecret(EncryptedSqliteConnectionFactory factory, string secret)
    {
        using var connection = factory.CreateOpenConnection();
        using var create = connection.CreateCommand();
        create.CommandText = "CREATE TABLE IF NOT EXISTS secrets (id INTEGER PRIMARY KEY, value TEXT NOT NULL);";
        create.ExecuteNonQuery();

        using var insert = connection.CreateCommand();
        insert.CommandText = "INSERT INTO secrets (id, value) VALUES (1, $value);";
        insert.Parameters.AddWithValue("$value", secret);
        insert.ExecuteNonQuery();
    }

    private static string ReadSecret(SqliteConnection connection)
    {
        using var read = connection.CreateCommand();
        read.CommandText = "SELECT value FROM secrets WHERE id = 1;";
        return (string)read.ExecuteScalar()!;
    }

    private static int IndexOf(byte[] haystack, byte[] needle)
    {
        for (var i = 0; i <= haystack.Length - needle.Length; i++)
        {
            var found = true;
            for (var j = 0; j < needle.Length; j++)
            {
                if (haystack[i + j] != needle[j])
                {
                    found = false;
                    break;
                }
            }

            if (found)
            {
                return i;
            }
        }

        return -1;
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
}
