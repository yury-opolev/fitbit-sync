using FluentAssertions;

namespace FitbitSync.Host.Tests;

// Phase 8: the key-protection master secret (used by the Linux PassphraseKeyProtector to wrap the key
// file) is resolved from an inline secret OR a mounted secret-file path, trimmed, with a fail-fast error
// when neither is supplied. Pure and OS-independent, so it is unit-tested directly here.
public sealed class KeyProtectorSecretResolverTests : IDisposable
{
    private readonly List<string> tempFiles = [];

    [Fact]
    public void Resolve_PrefersInlineSecret_Trimmed()
    {
        var resolved = KeyProtectorSecretResolver.Resolve("  inline-secret  ", "");

        resolved.Should().Be("inline-secret");
    }

    [Fact]
    public void Resolve_ReadsSecretFile_WhenInlineAbsent_Trimmed()
    {
        var path = this.WriteTempFile("  file-secret\n");

        var resolved = KeyProtectorSecretResolver.Resolve("", path);

        resolved.Should().Be("file-secret");
    }

    [Fact]
    public void Resolve_InlineWins_OverSecretFile()
    {
        var path = this.WriteTempFile("file-secret");

        var resolved = KeyProtectorSecretResolver.Resolve("inline-secret", path);

        resolved.Should().Be("inline-secret");
    }

    [Fact]
    public void Resolve_NeitherSupplied_ThrowsFailFast()
    {
        var act = () => KeyProtectorSecretResolver.Resolve("", "   ");

        act.Should().Throw<InvalidOperationException>().WithMessage("*KeyProtectorSecret*");
    }

    [Fact]
    public void Resolve_MissingSecretFile_ThrowsFailFast()
    {
        var missing = Path.Combine(Path.GetTempPath(), $"fitbitsync-missing-{Guid.NewGuid():N}.secret");

        var act = () => KeyProtectorSecretResolver.Resolve("", missing);

        act.Should().Throw<InvalidOperationException>().WithMessage("*does not exist*");
    }

    [Fact]
    public void Resolve_EmptySecretFile_ThrowsFailFast()
    {
        var path = this.WriteTempFile("   \n");

        var act = () => KeyProtectorSecretResolver.Resolve("", path);

        act.Should().Throw<InvalidOperationException>().WithMessage("*empty*");
    }

    public void Dispose()
    {
        foreach (var path in this.tempFiles)
        {
            if (File.Exists(path))
            {
                File.Delete(path);
            }
        }
    }

    private string WriteTempFile(string contents)
    {
        var path = Path.Combine(Path.GetTempPath(), $"fitbitsync-secret-{Guid.NewGuid():N}.secret");
        File.WriteAllText(path, contents);
        this.tempFiles.Add(path);
        return path;
    }
}
