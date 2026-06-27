using FitbitSync.Persistence;
using FitbitSync.Providers.Fitbit;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace FitbitSync.Host;

// Thin untested shell: orchestrates the one-time loopback OAuth login. Begin -> open browser ->
// await loopback redirect -> pre-check state (anti-CSRF) -> CompleteAsync (which re-checks state,
// exchanges the code, persists tokens, and audits). All branching logic worth testing already lives
// in the pure units this composes (OAuthStateValidator, LoopbackRedirectParser, the auth service).
internal static class LoginCommand
{
    public static async Task<int> ExecuteAsync(IHost host, CancellationToken ct = default)
    {
        using var scope = host.Services.CreateScope();
        var services = scope.ServiceProvider;

        services.GetRequiredService<ISchemaInitializer>().Initialize();

        var authorizationService = services.GetRequiredService<FitbitAuthorizationService>();
        var browser = services.GetRequiredService<IBrowserLauncher>();
        var listener = services.GetRequiredService<ILoopbackOAuthListener>();
        var oauthOptions = services.GetRequiredService<FitbitOAuthOptions>();

        var session = authorizationService.Begin();

        Console.WriteLine("Opening your browser to authorize FitbitSync...");
        browser.Open(session.AuthorizeUrl);

        var callback = await listener.WaitForCallbackAsync(oauthOptions.RedirectUri!, ct).ConfigureAwait(false);

        if (!callback.IsSuccess)
        {
            Console.Error.WriteLine($"Authorization failed: {callback.Error}");
            return 1;
        }

        if (!OAuthStateValidator.IsMatch(session.State, callback.State))
        {
            Console.Error.WriteLine("Authorization failed: state mismatch (possible CSRF).");
            return 1;
        }

        await authorizationService.CompleteAsync(session, callback.State!, callback.Code!, ct).ConfigureAwait(false);

        Console.WriteLine("Authorization complete. Tokens stored.");
        return 0;
    }
}
