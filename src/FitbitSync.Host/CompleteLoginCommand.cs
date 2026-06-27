using FitbitSync.Domain;
using FitbitSync.Persistence;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace FitbitSync.Host;

// Thin agent shell: headless login step 2. Loads the pending authorization, rejects an expired one, then
// validates the pasted callback URL — denial/missing params via LoopbackRedirectParser and the anti-CSRF
// state via OAuthStateValidator (both tested) — before exchanging the code for tokens through
// FitbitAuthorizationService. Clears the pending row on success and emits a JSON envelope. Each outcome maps
// to a stable exit code via HeadlessLoginResponse. Never prints tokens or the verifier.
internal static class CompleteLoginCommand
{
    public const string Name = HeadlessLoginResponse.CompleteCommand;

    public static async Task<int> ExecuteAsync(IHost host, CliOptions? options, CancellationToken ct = default)
    {
        using var scope = host.Services.CreateScope();
        var services = scope.ServiceProvider;

        services.GetRequiredService<ISchemaInitializer>().Initialize();

        var store = services.GetRequiredService<IPendingAuthorizationStore>();
        var clock = services.GetRequiredService<IClock>();

        var redirect = options?.Redirect;
        if (string.IsNullOrWhiteSpace(redirect) || !Uri.TryCreate(redirect, UriKind.Absolute, out var redirectUri))
        {
            return Emit(LoginCompletionStatus.InvalidRedirect);
        }

        var pending = await store.GetAsync(ct).ConfigureAwait(false);
        if (pending is null)
        {
            return Emit(LoginCompletionStatus.NoPendingAuthorization);
        }

        if (pending.IsExpired(clock.UtcNow))
        {
            await store.DeleteAsync(ct).ConfigureAwait(false);
            return Emit(LoginCompletionStatus.AuthorizationExpired);
        }

        var callback = LoopbackRedirectParser.Parse(redirectUri);
        if (!callback.IsSuccess)
        {
            return Emit(LoginCompletionStatus.AuthorizationDenied, callback.Error);
        }

        if (!OAuthStateValidator.IsMatch(pending.State, callback.State))
        {
            return Emit(LoginCompletionStatus.StateMismatch);
        }

        var authorization = services.GetRequiredService<IAuthorizationService>();
        var session = new AuthorizationSession(pending.AuthorizeUrl, pending.State, pending.CodeVerifier);

        try
        {
            await authorization.CompleteAsync(session, callback.State!, callback.Code!, ct).ConfigureAwait(false);
        }
        catch (ProviderAuthenticationException ex)
        {
            return Emit(LoginCompletionStatus.TokenExchangeFailed, ex.Message);
        }

        await store.DeleteAsync(ct).ConfigureAwait(false);
        return Emit(LoginCompletionStatus.Authorized);
    }

    private static int Emit(LoginCompletionStatus status, string? detail = null) =>
        AgentConsole.Emit(HeadlessLoginResponse.ForCompletion(status, detail));
}
