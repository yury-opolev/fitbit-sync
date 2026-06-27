using FitbitSync.Application;
using FitbitSync.Persistence;
using FitbitSync.Providers.Fitbit;
using FitbitSync.Security;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace FitbitSync.Host;

// Composition root. Registers the security/persistence-factory services that AddPersistence intentionally
// leaves to the host (decision: keys/passphrase come from config, never generated ephemerally), then layers
// AddPersistence + AddFitbitProvider + AddSyncEngine on top. FitbitOAuthOptions is mapped by hand because its
// Uri and IReadOnlyList<string> members do not round-trip cleanly through the default configuration binder.
public static class HostServiceCollectionExtensions
{
    private const string FitbitSection = "Fitbit";

    public static IServiceCollection AddFitbitSyncHost(this IServiceCollection services, IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configuration);

        var storage = BindStorageOptions(configuration);
        HostConfigurationValidator.ValidateStorage(storage);

        var configureOAuth = ApplyFitbitOAuthConfiguration(configuration);
        ValidateOAuthConfiguration(configureOAuth);

        services.AddSingleton<IKeyProvider>(_ => BuildKeyProvider(storage));
        services.AddSingleton<IColumnCipher>(sp =>
            new AesGcmColumnCipher(sp.GetRequiredService<IKeyProvider>().GetColumnEncryptionKey().ToArray()));
        services.AddSingleton<IRecordSigner, HmacRecordSigner>();
        services.AddSingleton(_ => new EncryptedSqliteConnectionFactory(storage.DatabasePath, storage.DatabasePassphrase));
        services.AddSingleton<EncryptedDbContextFactory>();

        services.AddPersistence();
        services.AddFitbitProvider(configureOAuth: configureOAuth);
        services.AddSyncEngine();

        services.AddSingleton<ILoopbackOAuthListener, HttpListenerLoopbackOAuthListener>();
        services.AddSingleton<IBrowserLauncher, SystemBrowserLauncher>();

        return services;
    }

    private static HostStorageOptions BindStorageOptions(IConfiguration configuration)
    {
        var storage = new HostStorageOptions();
        configuration.GetSection(HostStorageOptions.SectionName).Bind(storage);
        return storage;
    }

    private static void ValidateOAuthConfiguration(Action<FitbitOAuthOptions> configureOAuth)
    {
        var oauth = new FitbitOAuthOptions();
        configureOAuth(oauth);
        HostConfigurationValidator.ValidateOAuth(oauth);
    }

    private static IKeyProvider BuildKeyProvider(HostStorageOptions storage)
    {
        if (!string.IsNullOrWhiteSpace(storage.KeyFilePath))
        {
            return new DpapiProtectedKeyFileProvider(storage.KeyFilePath, SelectKeyProtector(storage));
        }

        var columnKey = StorageKeyDecoder.Decode(storage.ColumnEncryptionKeyBase64, nameof(storage.ColumnEncryptionKeyBase64));
        var signingKey = StorageKeyDecoder.Decode(storage.SigningKeyBase64, nameof(storage.SigningKeyBase64));
        return new InMemoryKeyProvider(columnKey, signingKey);
    }

    private static IKeyProtector SelectKeyProtector(HostStorageOptions storage)
    {
        if (OperatingSystem.IsWindows())
        {
            return new DpapiKeyProtector();
        }

        var masterSecret = KeyProtectorSecretResolver.Resolve(storage.KeyProtectorSecret, storage.KeyProtectorSecretFile);
        return new PassphraseKeyProtector(masterSecret);
    }

    private static Action<FitbitOAuthOptions> ApplyFitbitOAuthConfiguration(IConfiguration configuration)
    {
        var section = configuration.GetSection(FitbitSection);

        return options =>
        {
            options.ClientId = section["ClientId"] ?? "";
            options.ClientSecret = section["ClientSecret"];

            var redirectUri = section["RedirectUri"];
            if (!string.IsNullOrWhiteSpace(redirectUri))
            {
                options.RedirectUri = new Uri(redirectUri, UriKind.Absolute);
            }

            var scopes = section.GetSection("Scopes").Get<string[]>();
            if (scopes is { Length: > 0 })
            {
                options.Scopes = scopes;
            }
        };
    }
}
