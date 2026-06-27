using Microsoft.EntityFrameworkCore;

namespace FitbitSync.Persistence;

public sealed class EncryptedDbContextFactory
{
    private readonly EncryptedSqliteConnectionFactory connectionFactory;

    public EncryptedDbContextFactory(EncryptedSqliteConnectionFactory connectionFactory)
    {
        ArgumentNullException.ThrowIfNull(connectionFactory);
        this.connectionFactory = connectionFactory;
    }

    public FitbitSyncDbContext Create()
    {
        var connection = this.connectionFactory.CreateOpenConnection();

        var options = new DbContextOptionsBuilder<FitbitSyncDbContext>()
            .UseSqlite(connection, contextOwnsConnection: true)
            .Options;

        return new FitbitSyncDbContext(options);
    }
}
