using FitbitSync.Persistence;
using FitbitSync.Security;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace FitbitSync.Host;

// Thin untested shell: reads the NEW key material from configuration (Storage:New* keys via User Secrets
// or environment variables), builds the new IKeyProvider, then delegates to KeyRotationService, which
// re-signs every sample under the new signing key, updates schema_metadata, audits, and PRAGMA-rekeys the
// database file. Key decoding (StorageKeyDecoder) and the rotation itself (KeyRotationService) are tested.
internal static class RotateKeysCommand
{
    public static async Task<int> ExecuteAsync(IHost host, CancellationToken ct = default)
    {
        var configuration = host.Services.GetRequiredService<IConfiguration>();
        var storage = new HostStorageOptions();
        configuration.GetSection(HostStorageOptions.SectionName).Bind(storage);

        var newColumnKey = StorageKeyDecoder.Decode(storage.NewColumnEncryptionKeyBase64, nameof(storage.NewColumnEncryptionKeyBase64));
        var newSigningKey = StorageKeyDecoder.Decode(storage.NewSigningKeyBase64, nameof(storage.NewSigningKeyBase64));
        var newKeyProvider = new InMemoryKeyProvider(newColumnKey, newSigningKey);

        using var scope = host.Services.CreateScope();
        var services = scope.ServiceProvider;

        services.GetRequiredService<ISchemaInitializer>().Initialize();

        var service = services.GetRequiredService<KeyRotationService>();
        var newPassphrase = string.IsNullOrWhiteSpace(storage.NewDatabasePassphrase) ? null : storage.NewDatabasePassphrase;
        var result = await service.RotateAsync(newKeyProvider, newPassphrase, ct).ConfigureAwait(false);

        Console.WriteLine($"Re-signed samples:    {result.ResignedSampleCount}");
        Console.WriteLine($"New signing key id:   {result.NewSigningKeyId}");
        Console.WriteLine($"Database re-encrypted: {result.DatabaseRekeyed}");
        Console.WriteLine("Key rotation complete. Update Storage:SigningKeyBase64 / ColumnEncryptionKeyBase64 / DatabasePassphrase to the new values before the next run.");
        return 0;
    }
}
