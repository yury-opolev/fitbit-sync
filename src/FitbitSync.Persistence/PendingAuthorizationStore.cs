using FitbitSync.Domain;
using Microsoft.EntityFrameworkCore;

namespace FitbitSync.Persistence;

// EF Core-backed pending-authorization store. Single-row (Id = 1): SaveAsync upserts so a new login
// replaces any prior pending one; GetAsync reads it back; DeleteAsync clears it on completion or
// expiry. The row lives in the whole-file-encrypted database.
public sealed class PendingAuthorizationStore : IPendingAuthorizationStore
{
    private const int SingleRowId = 1;

    private readonly FitbitSyncDbContext dbContext;

    public PendingAuthorizationStore(FitbitSyncDbContext dbContext)
    {
        ArgumentNullException.ThrowIfNull(dbContext);

        this.dbContext = dbContext;
    }

    public async Task SaveAsync(PendingAuthorization pending, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(pending);

        var existing = await this.dbContext.PendingAuthorizations
            .SingleOrDefaultAsync(row => row.Id == SingleRowId, ct)
            .ConfigureAwait(false);

        if (existing is null)
        {
            this.dbContext.PendingAuthorizations.Add(new PendingAuthorizationRow
            {
                Id = SingleRowId,
                State = pending.State,
                CodeVerifier = pending.CodeVerifier,
                AuthorizeUrl = pending.AuthorizeUrl.ToString(),
                ExpiresAt = pending.ExpiresAt,
            });
        }
        else
        {
            existing.State = pending.State;
            existing.CodeVerifier = pending.CodeVerifier;
            existing.AuthorizeUrl = pending.AuthorizeUrl.ToString();
            existing.ExpiresAt = pending.ExpiresAt;
        }

        await this.dbContext.SaveChangesAsync(ct).ConfigureAwait(false);
    }

    public async Task<PendingAuthorization?> GetAsync(CancellationToken ct = default)
    {
        var existing = await this.dbContext.PendingAuthorizations
            .SingleOrDefaultAsync(row => row.Id == SingleRowId, ct)
            .ConfigureAwait(false);

        if (existing is null)
        {
            return null;
        }

        return new PendingAuthorization(
            existing.State,
            existing.CodeVerifier,
            new Uri(existing.AuthorizeUrl, UriKind.Absolute),
            existing.ExpiresAt);
    }

    public async Task DeleteAsync(CancellationToken ct = default)
    {
        var existing = await this.dbContext.PendingAuthorizations
            .SingleOrDefaultAsync(row => row.Id == SingleRowId, ct)
            .ConfigureAwait(false);

        if (existing is not null)
        {
            this.dbContext.PendingAuthorizations.Remove(existing);
            await this.dbContext.SaveChangesAsync(ct).ConfigureAwait(false);
        }
    }
}
