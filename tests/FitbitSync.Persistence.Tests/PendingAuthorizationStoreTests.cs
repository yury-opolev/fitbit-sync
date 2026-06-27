using FitbitSync.Domain;
using FluentAssertions;

namespace FitbitSync.Persistence.Tests;

// The pending-authorization store holds the single in-flight PKCE login between `login --begin`
// and `login --complete`. It round-trips through the encrypted database, keeps at most one row (a
// new login replaces any prior pending one), and is cleared on completion. Each operation uses a
// FRESH DbContext so the database — not one change-tracker — is exercised.
public sealed class PendingAuthorizationStoreTests : IDisposable
{
    private readonly EncryptedDatabaseFixture fixture = new();

    private static readonly DateTimeOffset ExpiresAt = new(2024, 5, 1, 12, 0, 0, TimeSpan.Zero);

    private static PendingAuthorization Create(string state = "state-1", string verifier = "verifier-1") =>
        new(state, verifier, new Uri("https://www.fitbit.com/oauth2/authorize?response_type=code"), ExpiresAt);

    private async Task SaveThroughFreshContextAsync(PendingAuthorization pending)
    {
        using var context = this.fixture.NewDbContext();
        var store = new PendingAuthorizationStore(context);
        await store.SaveAsync(pending);
    }

    [Fact]
    public async Task SaveThenGet_RoundTripsPendingAuthorization()
    {
        var pending = Create();
        await this.SaveThroughFreshContextAsync(pending);

        using var context = this.fixture.NewDbContext();
        var store = new PendingAuthorizationStore(context);
        var loaded = await store.GetAsync();

        loaded.Should().NotBeNull();
        loaded!.State.Should().Be(pending.State);
        loaded.CodeVerifier.Should().Be(pending.CodeVerifier);
        loaded.AuthorizeUrl.Should().Be(pending.AuthorizeUrl);
        loaded.ExpiresAt.Should().Be(pending.ExpiresAt);
    }

    [Fact]
    public async Task Get_ReturnsNull_WhenNoPendingAuthorization()
    {
        using var context = this.fixture.NewDbContext();
        var store = new PendingAuthorizationStore(context);

        var loaded = await store.GetAsync();

        loaded.Should().BeNull();
    }

    [Fact]
    public async Task Save_ReplacesAnyExistingPendingAuthorization()
    {
        await this.SaveThroughFreshContextAsync(Create("state-old", "verifier-old"));
        await this.SaveThroughFreshContextAsync(Create("state-new", "verifier-new"));

        using var context = this.fixture.NewDbContext();
        var store = new PendingAuthorizationStore(context);
        var loaded = await store.GetAsync();

        loaded.Should().NotBeNull();
        loaded!.State.Should().Be("state-new");
        loaded.CodeVerifier.Should().Be("verifier-new");

        // Only one pending row is ever retained.
        context.PendingAuthorizations.Count().Should().Be(1);
    }

    [Fact]
    public async Task Delete_RemovesPendingAuthorization()
    {
        await this.SaveThroughFreshContextAsync(Create());

        using (var context = this.fixture.NewDbContext())
        {
            var store = new PendingAuthorizationStore(context);
            await store.DeleteAsync();
        }

        using var verifyContext = this.fixture.NewDbContext();
        var verifyStore = new PendingAuthorizationStore(verifyContext);
        (await verifyStore.GetAsync()).Should().BeNull();
    }

    [Fact]
    public async Task Delete_IsNoOp_WhenNothingPending()
    {
        using var context = this.fixture.NewDbContext();
        var store = new PendingAuthorizationStore(context);

        var act = async () => await store.DeleteAsync();

        await act.Should().NotThrowAsync();
    }

    public void Dispose() => this.fixture.Dispose();
}
