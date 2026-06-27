using FitbitSync.Domain;
using FitbitSync.Providers.Fitbit;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace FitbitSync.Providers.Fitbit.Tests;

// AddFitbitProvider wires the whole adapter — the typed client (with configured BaseAddress), the
// bearer + rate-limit handlers, the refreshing access-token source, the OAuth/PKCE services, and
// IHealthDataProvider — and fails fast on invalid provider OR OAuth options. Prerequisite ports
// (ITokenStore, IClock, IAuditTrail) are stubbed.
public sealed class AddFitbitProviderTests
{
    private static readonly Uri CustomBaseAddress = new("https://fitbit.test/");

    private static ServiceCollection ServicesWithPrerequisites()
    {
        var services = new ServiceCollection();
        services.AddSingleton<IClock, StubClock>();
        services.AddScoped<ITokenStore, StubTokenStore>();
        services.AddScoped<IAuditTrail, StubAuditTrail>();
        return services;
    }

    private static void ValidOAuth(FitbitOAuthOptions oauth)
    {
        oauth.ClientId = "test-client";
        oauth.RedirectUri = new Uri("http://127.0.0.1:7890/v1/oauth/callback");
    }

    [Fact]
    public void AddFitbitProvider_RegistersProvider_AndTypedClient()
    {
        var services = ServicesWithPrerequisites();

        services.AddFitbitProvider(options => options.BaseAddress = CustomBaseAddress, ValidOAuth);

        using var provider = services.BuildServiceProvider();
        using var scope = provider.CreateScope();
        var resolver = scope.ServiceProvider;

        resolver.GetRequiredService<IHealthDataProvider>().Should().BeOfType<FitbitHealthDataProvider>();
        resolver.GetRequiredService<FitbitApiClient>().Should().NotBeNull();
        resolver.GetRequiredService<IAccessTokenSource>().Should().BeOfType<RefreshingAccessTokenSource>();

        var httpClient = resolver.GetRequiredService<IHttpClientFactory>().CreateClient(nameof(FitbitApiClient));
        httpClient.BaseAddress.Should().Be(CustomBaseAddress);
    }

    [Fact]
    public void AddFitbitProvider_FailsFast_OnInvalidBaseAddress()
    {
        var services = ServicesWithPrerequisites();

        services.AddFitbitProvider(options => options.BaseAddress = new Uri("/relative", UriKind.Relative), ValidOAuth);

        using var provider = services.BuildServiceProvider();
        using var scope = provider.CreateScope();

        var resolveOptions = () => scope.ServiceProvider.GetRequiredService<IOptions<FitbitProviderOptions>>().Value;

        resolveOptions.Should().Throw<OptionsValidationException>();
    }

    [Fact]
    public void AddFitbitProvider_FailsFast_OnMissingOAuthClientId()
    {
        var services = ServicesWithPrerequisites();

        services.AddFitbitProvider(options => options.BaseAddress = CustomBaseAddress, oauth => oauth.ClientId = "");

        using var provider = services.BuildServiceProvider();
        using var scope = provider.CreateScope();

        var resolveOAuth = () => scope.ServiceProvider.GetRequiredService<FitbitOAuthOptions>();

        resolveOAuth.Should().Throw<OptionsValidationException>();
    }

    private sealed class StubClock : IClock
    {
        public DateTimeOffset UtcNow => new(2024, 5, 10, 12, 0, 0, TimeSpan.Zero);
    }

    private sealed class StubTokenStore : ITokenStore
    {
        public Task<OAuthToken?> LoadAsync(CancellationToken ct = default) => Task.FromResult<OAuthToken?>(null);

        public Task SaveAsync(OAuthToken token, CancellationToken ct = default) => Task.CompletedTask;
    }

    private sealed class StubAuditTrail : IAuditTrail
    {
        public Task<AuditEntry> AppendAsync(string action, CancellationToken ct = default)
            => Task.FromResult(new AuditEntry(1, DateTimeOffset.UnixEpoch, action, "", ""));

        public Task<bool> VerifyChainAsync(CancellationToken ct = default) => Task.FromResult(true);
    }
}
