using FitbitSync.Domain;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace FitbitSync.Providers.Fitbit;

public static class FitbitProviderServiceCollectionExtensions
{
    public static IServiceCollection AddFitbitProvider(
        this IServiceCollection services,
        Action<FitbitProviderOptions>? configure = null,
        Action<FitbitOAuthOptions>? configureOAuth = null)
    {
        ArgumentNullException.ThrowIfNull(services);

        var optionsBuilder = services.AddOptions<FitbitProviderOptions>();
        if (configure is not null)
        {
            optionsBuilder.Configure(configure);
        }

        optionsBuilder
            .Validate(options => options.BaseAddress is { IsAbsoluteUri: true }, "Fitbit BaseAddress must be an absolute URI.")
            .Validate(options => options.RequestTimeout > TimeSpan.Zero, "Fitbit RequestTimeout must be positive.")
            .ValidateOnStart();

        // OAuth options: validated here (Phase 4); the actual values are bound by the composition root (Phase 6).
        var oauthBuilder = services.AddOptions<FitbitOAuthOptions>();
        if (configureOAuth is not null)
        {
            oauthBuilder.Configure(configureOAuth);
        }

        oauthBuilder
            .Validate(options => !string.IsNullOrWhiteSpace(options.ClientId), "Fitbit ClientId is required.")
            .Validate(options => options.RedirectUri is { IsAbsoluteUri: true }, "Fitbit RedirectUri must be an absolute URI.")
            .ValidateOnStart();

        // Bridge the validated options value to the concrete type the OAuth services inject by ctor.
        services.AddSingleton(sp => sp.GetRequiredService<IOptions<FitbitOAuthOptions>>().Value);

        // PKCE primitives.
        services.AddSingleton<IRandomBytesGenerator, CryptoRandomBytesGenerator>();
        services.AddTransient<PkceGenerator>();
        services.AddTransient<AuthorizeUrlBuilder>();

        // Single-flight refresh coordinator is process-wide (singleton) so concurrent refreshes collapse to one.
        services.AddSingleton<ITokenRefreshCoordinator, TokenRefreshCoordinator>();

        // The token client mints/refreshes tokens, so it must NOT carry the BearerTokenHandler (that would be circular).
        services.AddHttpClient<FitbitTokenClient>();

        // Active access-token source now refreshes (proactive skew + reactive 401) instead of being read-only.
        services.AddScoped<IAccessTokenSource, RefreshingAccessTokenSource>();

        services.AddTransient<BearerTokenHandler>();
        services.AddTransient<RateLimitHandler>();

        services.AddSingleton<RateLimitSnapshotHolder>();

        services.AddHttpClient<FitbitApiClient>((provider, client) =>
            {
                var options = provider.GetRequiredService<IOptions<FitbitProviderOptions>>().Value;
                client.BaseAddress = options.BaseAddress;
                client.Timeout = options.RequestTimeout;
            })
            .AddHttpMessageHandler<BearerTokenHandler>()
            .AddHttpMessageHandler<RateLimitHandler>();

        services.AddScoped(sp => new FitbitAuthorizationService(
            sp.GetRequiredService<PkceGenerator>(),
            sp.GetRequiredService<AuthorizeUrlBuilder>(),
            sp.GetRequiredService<IRandomBytesGenerator>(),
            sp.GetRequiredService<FitbitTokenClient>(),
            sp.GetRequiredService<ITokenStore>(),
            sp.GetRequiredService<IAuditTrail>(),
            sp.GetRequiredService<FitbitOAuthOptions>()));
        services.AddScoped<IAuthorizationService>(sp => sp.GetRequiredService<FitbitAuthorizationService>());

        services.AddScoped<IHealthDataProvider, FitbitHealthDataProvider>();

        return services;
    }
}
