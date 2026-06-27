using FitbitSync.Domain;
using Microsoft.Extensions.DependencyInjection;

namespace FitbitSync.Providers.GoogleHealth;

// Registers the Google Health provider: OAuth options + login services (the provider-neutral
// IAuthorizationService), the refreshing access-token source, and the typed Health API client behind
// IHealthDataProvider. The host supplies configuration via the configureOAuth callback.
public static class GoogleProviderServiceCollectionExtensions
{
    public static IServiceCollection AddGoogleHealthProvider(this IServiceCollection services, Action<GoogleOAuthOptions> configureOAuth)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configureOAuth);

        var options = new GoogleOAuthOptions();
        configureOAuth(options);
        services.AddSingleton(options);

        services.AddTransient<GoogleAuthorizeUrlBuilder>();
        services.AddHttpClient<GoogleTokenClient>();

        services.AddScoped<GoogleAccessTokenSource>();
        services.AddScoped<GoogleAuthorizationService>();
        services.AddScoped<IAuthorizationService>(sp => sp.GetRequiredService<GoogleAuthorizationService>());

        services.AddHttpClient<GoogleHealthApiClient>(client => client.BaseAddress = GoogleHealthApiClient.BaseAddress);
        services.AddScoped<IHealthDataProvider, GoogleHealthDataProvider>();

        return services;
    }
}
