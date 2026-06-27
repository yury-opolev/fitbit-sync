using FitbitSync.Security;

namespace FitbitSync.Persistence;

public sealed class SchemaInitializer : ISchemaInitializer
{
    public const string EncryptionSchemeVersion = "1";

    private const string EncryptionSchemeKey = "encryption_scheme_version";
    public const string SigningKeyIdMetadataKey = "signing_key_id";

    private readonly FitbitSyncDbContext dbContext;
    private readonly IKeyProvider keyProvider;

    public SchemaInitializer(FitbitSyncDbContext dbContext, IKeyProvider keyProvider)
    {
        ArgumentNullException.ThrowIfNull(dbContext);
        ArgumentNullException.ThrowIfNull(keyProvider);

        this.dbContext = dbContext;
        this.keyProvider = keyProvider;
    }

    public void Initialize()
    {
        this.dbContext.Database.EnsureCreated();
        this.UpsertMetadata(EncryptionSchemeKey, EncryptionSchemeVersion);
        this.UpsertMetadata(SigningKeyIdMetadataKey, this.keyProvider.SigningKeyId);
        this.dbContext.SaveChanges();
    }

    private void UpsertMetadata(string key, string value)
    {
        var existing = this.dbContext.SchemaMetadata.Find(key);
        if (existing is null)
        {
            this.dbContext.SchemaMetadata.Add(new SchemaMetadataRow { Key = key, Value = value });
        }
        else
        {
            existing.Value = value;
        }
    }
}
