namespace FitbitSync.Host;

// Opens a URL in the user's default browser. Thin shell over the OS shell-execute facility.
public interface IBrowserLauncher
{
    void Open(Uri url);
}
