using System.Security.Cryptography;
using FitbitSync.Application;
using FitbitSync.Domain;
using FitbitSync.Persistence;
using FitbitSync.Providers.Fitbit;
using FitbitSync.Security;
using FluentAssertions;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace FitbitSync.Host.Tests;

// Composition-root smoke tests: AddFitbitSyncHost wires the security/persistence-factory seams the host
// owns, then layers persistence + provider + sync engine. We assert key registrations resolve (without
// opening the encrypted DB) and that missing/invalid secrets fail fast.
public sealed class HostServiceCollectionExtensionsTests
{
    private static IConfiguration BuildConfiguration(IDictionary<string, string?>? overrides = null)
    {
        var settings = new Dictionary<string, string?>
        {
            ["Storage:DatabasePath"] = Path.Combine(Path.GetTempPath(), $"fitbitsync-host-test-{Guid.NewGuid():N}.db"),
            ["Storage:DatabasePassphrase"] = "test-passphrase",
            ["Storage:ColumnEncryptionKeyBase64"] = Convert.ToBase64String(RandomNumberGenerator.GetBytes(32)),
            ["Storage:SigningKeyBase64"] = Convert.ToBase64String(RandomNumberGenerator.GetBytes(32)),
            ["Fitbit:ClientId"] = "test-client-id",
            ["Fitbit:RedirectUri"] = "http://127.0.0.1:7654/callback",
            ["Fitbit:Scopes:0"] = "heartrate",
            ["Fitbit:Scopes:1"] = "sleep",
        };

        if (overrides is not null)
        {
            foreach (var pair in overrides)
            {
                settings[pair.Key] = pair.Value;
            }
        }

        return new ConfigurationBuilder().AddInMemoryCollection(settings).Build();
    }

    private static ServiceProvider BuildProvider(IConfiguration configuration)
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddFitbitSyncHost(configuration);
        return services.BuildServiceProvider(new ServiceProviderOptions { ValidateOnBuild = false });
    }

    [Fact]
    public void AddFitbitSyncHost_RegistersSecurityAndKeySeams()
    {
        using var provider = BuildProvider(BuildConfiguration());

        provider.GetRequiredService<IKeyProvider>().Should().BeOfType<InMemoryKeyProvider>();
        provider.GetRequiredService<IColumnCipher>().Should().BeOfType<AesGcmColumnCipher>();
        provider.GetRequiredService<IRecordSigner>().Should().BeOfType<HmacRecordSigner>();
        provider.GetRequiredService<EncryptedSqliteConnectionFactory>().Should().NotBeNull();
        provider.GetRequiredService<EncryptedDbContextFactory>().Should().NotBeNull();
    }

    [Fact]
    public void AddFitbitSyncHost_RegistersSyncSchedulerAsHostedService()
    {
        using var provider = BuildProvider(BuildConfiguration());

        provider.GetServices<IHostedService>().Should().ContainSingle(service => service is SyncScheduler);
    }

    [Fact]
    public void AddFitbitSyncHost_RegistersLoopbackAndBrowserShells()
    {
        using var provider = BuildProvider(BuildConfiguration());

        provider.GetRequiredService<ILoopbackOAuthListener>().Should().BeOfType<HttpListenerLoopbackOAuthListener>();
        provider.GetRequiredService<IBrowserLauncher>().Should().BeOfType<SystemBrowserLauncher>();
    }

    [Fact]
    public void AddFitbitSyncHost_BindsFitbitOAuthOptionsFromConfiguration()
    {
        using var provider = BuildProvider(BuildConfiguration());

        var options = provider.GetRequiredService<FitbitOAuthOptions>();

        options.ClientId.Should().Be("test-client-id");
        options.RedirectUri.Should().Be(new Uri("http://127.0.0.1:7654/callback"));
        options.Scopes.Should().BeEquivalentTo("heartrate", "sleep");
    }

    [Fact]
    public void AddFitbitSyncHost_ResolvesAuthorizationServiceAndProvider()
    {
        using var provider = BuildProvider(BuildConfiguration());
        using var scope = provider.CreateScope();

        scope.ServiceProvider.GetRequiredService<FitbitAuthorizationService>().Should().NotBeNull();
        scope.ServiceProvider.GetRequiredService<IHealthDataProvider>().Should().BeOfType<FitbitHealthDataProvider>();
    }

    [Fact]
    public void AddFitbitSyncHost_MissingColumnKey_ThrowsFailFast()
    {
        var configuration = BuildConfiguration(new Dictionary<string, string?> { ["Storage:ColumnEncryptionKeyBase64"] = "" });

        var act = () =>
        {
            using var provider = BuildProvider(configuration);
            provider.GetRequiredService<IKeyProvider>();
        };

        act.Should().Throw<InvalidOperationException>().WithMessage("*ColumnEncryptionKeyBase64*");
    }

    [Fact]
    public void AddFitbitSyncHost_InvalidBase64Key_ThrowsFailFast()
    {
        var configuration = BuildConfiguration(new Dictionary<string, string?> { ["Storage:SigningKeyBase64"] = "not-base64!!!" });

        var act = () =>
        {
            using var provider = BuildProvider(configuration);
            provider.GetRequiredService<IKeyProvider>();
        };

        act.Should().Throw<InvalidOperationException>().WithMessage("*SigningKeyBase64*");
    }
}
