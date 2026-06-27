using System.Security.Cryptography;
using System.Text;

namespace FitbitSync.Host;

// Anti-CSRF: the state returned on the loopback redirect must equal the opaque state we issued in
// FitbitAuthorizationSession. A fixed-time comparison avoids leaking match length via timing. The
// authorization service repeats this check before exchanging the code; validating here lets the host
// reject a forged callback before doing any further work.
public static class OAuthStateValidator
{
    public static bool IsMatch(string? expectedState, string? returnedState)
    {
        if (string.IsNullOrEmpty(expectedState) || string.IsNullOrEmpty(returnedState))
        {
            return false;
        }

        var expectedBytes = Encoding.UTF8.GetBytes(expectedState);
        var returnedBytes = Encoding.UTF8.GetBytes(returnedState);

        return CryptographicOperations.FixedTimeEquals(expectedBytes, returnedBytes);
    }
}
