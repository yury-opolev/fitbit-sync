using System.Security.Cryptography;
using FitbitSync.Providers.GoogleHealth;
using FluentAssertions;

namespace FitbitSync.Host.Tests;

// Phase 7 config validators: startup must fail fast (with a named, actionable error) when required
// storage or OAuth configuration is missing or malformed, so the service never silently runs
// unconfigured. These exercise the pure validator directly.
public sealed class HostConfigurationValidatorTests
{
    private static HostStorageOptions ValidStorage() => new()
    {
        DatabasePath = "fitbitsync.db",
        DatabasePassphrase = "strong-passphrase",
        ColumnEncryptionKeyBase64 = Convert.ToBase64String(RandomNumberGenerator.GetBytes(32)),
        SigningKeyBase64 = Convert.ToBase64String(RandomNumberGenerator.GetBytes(32)),
    };

    private static GoogleOAuthOptions ValidOAuth() => new()
    {
        ClientId = "client-id",
        ClientSecret = "client-secret",
        RedirectUri = new Uri("https://localhost:7654/callback"),
        Scopes = ["https://www.googleapis.com/auth/googlehealth.sleep.readonly"],
    };

    [Fact]
    public void ValidateStorage_Passes_ForCompleteConfiguration()
    {
        var act = () => HostConfigurationValidator.ValidateStorage(ValidStorage());

        act.Should().NotThrow();
    }

    [Fact]
    public void ValidateStorage_MissingDatabasePath_Throws()
    {
        var storage = ValidStorage();
        storage.DatabasePath = "";

        var act = () => HostConfigurationValidator.ValidateStorage(storage);

        act.Should().Throw<InvalidOperationException>().WithMessage("*DatabasePath*");
    }

    [Fact]
    public void ValidateStorage_MissingPassphrase_Throws()
    {
        var storage = ValidStorage();
        storage.DatabasePassphrase = "   ";

        var act = () => HostConfigurationValidator.ValidateStorage(storage);

        act.Should().Throw<InvalidOperationException>().WithMessage("*DatabasePassphrase*");
    }

    [Fact]
    public void ValidateStorage_WrongLengthKey_Throws()
    {
        var storage = ValidStorage();
        storage.SigningKeyBase64 = Convert.ToBase64String(RandomNumberGenerator.GetBytes(16));

        var act = () => HostConfigurationValidator.ValidateStorage(storage);

        act.Should().Throw<InvalidOperationException>().WithMessage("*SigningKeyBase64*32 bytes*");
    }

    [Fact]
    public void ValidateStorage_WithKeyFile_DoesNotRequireBase64Keys()
    {
        // A configured key file replaces the base64 column/signing keys; validation must accept it.
        var storage = ValidStorage();
        storage.KeyFilePath = Path.Combine(Path.GetTempPath(), "fitbitsync.key");
        storage.ColumnEncryptionKeyBase64 = "";
        storage.SigningKeyBase64 = "";

        if (OperatingSystem.IsWindows())
        {
            // Windows wraps the key file with DPAPI — no master secret needed.
            var act = () => HostConfigurationValidator.ValidateStorage(storage);
            act.Should().NotThrow();
        }
        else
        {
            // Non-Windows wraps with the PassphraseKeyProtector — a master secret is required...
            var withoutSecret = () => HostConfigurationValidator.ValidateStorage(storage);
            withoutSecret.Should().Throw<InvalidOperationException>().WithMessage("*KeyProtectorSecret*");

            // ...and is accepted once supplied.
            storage.KeyProtectorSecret = "master-secret";
            var withSecret = () => HostConfigurationValidator.ValidateStorage(storage);
            withSecret.Should().NotThrow();
        }
    }

    [Fact]
    public void ValidateOAuth_Passes_ForCompleteConfiguration()
    {
        var act = () => HostConfigurationValidator.ValidateOAuth(ValidOAuth());

        act.Should().NotThrow();
    }

    [Fact]
    public void ValidateOAuth_MissingClientId_Throws()
    {
        var oauth = ValidOAuth();
        oauth.ClientId = "";

        var act = () => HostConfigurationValidator.ValidateOAuth(oauth);

        act.Should().Throw<InvalidOperationException>().WithMessage("*ClientId*");
    }

    [Fact]
    public void ValidateOAuth_MissingRedirectUri_Throws()
    {
        var oauth = ValidOAuth();
        oauth.RedirectUri = null;

        var act = () => HostConfigurationValidator.ValidateOAuth(oauth);

        act.Should().Throw<InvalidOperationException>().WithMessage("*RedirectUri*");
    }

    [Fact]
    public void ValidateOAuth_MissingClientSecret_Throws()
    {
        var oauth = ValidOAuth();
        oauth.ClientSecret = "";

        var act = () => HostConfigurationValidator.ValidateOAuth(oauth);

        act.Should().Throw<InvalidOperationException>().WithMessage("*ClientSecret*");
    }

    [Fact]
    public void ValidateOAuth_EmptyScopes_Throws()
    {
        var oauth = ValidOAuth();
        oauth.Scopes = [];

        var act = () => HostConfigurationValidator.ValidateOAuth(oauth);

        act.Should().Throw<InvalidOperationException>().WithMessage("*Scopes*");
    }
}
