using Microsoft.Data.Sqlite;

namespace FitbitSync.Persistence;

public sealed class EncryptedSqliteConnectionFactory
{
    private static int isBundleInitialized;

    private readonly string databasePath;
    private readonly string encryptionKey;

    public EncryptedSqliteConnectionFactory(string databasePath, string encryptionKey)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(databasePath);
        ArgumentException.ThrowIfNullOrWhiteSpace(encryptionKey);

        EnsureBundleInitialized();

        this.databasePath = databasePath;
        this.encryptionKey = encryptionKey;
    }

    public string DatabasePath => this.databasePath;

    public SqliteConnection CreateOpenConnection()
    {
        var connection = new SqliteConnection(this.BuildConnectionString(this.encryptionKey));
        connection.Open();
        return connection;
    }

    public SqliteConnection CreateOpenConnectionWithKey(string key)
    {
        var connection = new SqliteConnection(this.BuildConnectionString(key));
        connection.Open();
        return connection;
    }

    public SqliteConnection CreateOpenConnectionWithoutKey()
    {
        var builder = new SqliteConnectionStringBuilder
        {
            DataSource = this.databasePath,
            Mode = SqliteOpenMode.ReadWrite,
            Pooling = false,
        };

        var connection = new SqliteConnection(builder.ConnectionString);
        connection.Open();
        return connection;
    }

    public void Rekey(SqliteConnection connection, string newKey)
    {
        ArgumentNullException.ThrowIfNull(connection);
        ArgumentException.ThrowIfNullOrWhiteSpace(newKey);

        using var command = connection.CreateCommand();
        command.CommandText = "PRAGMA rekey = " + QuoteLiteral(newKey) + ";";
        command.ExecuteNonQuery();
    }

    private static string QuoteLiteral(string value) =>
        "'" + value.Replace("'", "''", StringComparison.Ordinal) + "'";

    private string BuildConnectionString(string key)
    {
        var builder = new SqliteConnectionStringBuilder
        {
            DataSource = this.databasePath,
            Mode = SqliteOpenMode.ReadWriteCreate,
            Password = key,
            Pooling = false,
        };

        return builder.ConnectionString;
    }

    private static void EnsureBundleInitialized()
    {
        if (Interlocked.Exchange(ref isBundleInitialized, 1) == 0)
        {
            SQLitePCL.Batteries_V2.Init();
        }
    }
}
