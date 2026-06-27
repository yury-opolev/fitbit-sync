using FitbitSync.Domain;
using FitbitSync.Persistence;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace FitbitSync.Host;

// Thin agent shell: headless login step 1. Starts a PKCE login, persists the single pending authorization
// (state + verifier + authorize URL) with a short TTL, and emits the authorize URL as a JSON envelope for a
// human to open and approve. Never prints tokens or the verifier. Pairs with `login --complete`.
internal static class BeginLoginCommand
{
    public const string Name = HeadlessLoginResponse.BeginCommand;

    private static readonly TimeSpan PendingAuthorizationTtl = TimeSpan.FromMinutes(15);

    public static async Task<int> ExecuteAsync(IHost host, CliOptions? options, CancellationToken ct = default)
    {
        using var scope = host.Services.CreateScope();
        var services = scope.ServiceProvider;

        services.GetRequiredService<ISchemaInitializer>().Initialize();

        var authorization = services.GetRequiredService<IAuthorizationService>();
        var store = services.GetRequiredService<IPendingAuthorizationStore>();
        var clock = services.GetRequiredService<IClock>();

        var session = authorization.Begin();
        var expiresAt = clock.UtcNow + PendingAuthorizationTtl;

        await store.SaveAsync(
            new PendingAuthorization(session.State, session.CodeVerifier, session.AuthorizeUrl, expiresAt),
            ct).ConfigureAwait(false);

        var data = new
        {
            authorizeUrl = session.AuthorizeUrl.ToString(),
            state = session.State,
            expiresAtUtc = expiresAt,
        };

        return AgentConsole.Emit(AgentResponse.Success(Name, AgentExitCode.Success, data));
    }
}
