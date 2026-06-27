using System.Diagnostics;

namespace FitbitSync.Host;

// Thin untested shell: launches the default browser via the OS shell. ProcessStartInfo.UseShellExecute
// resolves the registered handler for the URL scheme cross-platform.
internal sealed class SystemBrowserLauncher : IBrowserLauncher
{
    public void Open(Uri url)
    {
        ArgumentNullException.ThrowIfNull(url);

        var startInfo = new ProcessStartInfo
        {
            FileName = url.ToString(),
            UseShellExecute = true,
        };

        using var process = Process.Start(startInfo);
    }
}
