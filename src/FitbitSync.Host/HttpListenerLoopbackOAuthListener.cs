using System.Net;
using System.Text;

namespace FitbitSync.Host;

// Thin untested shell: binds an HttpListener to the loopback redirect URI, accepts the single OAuth
// redirect, writes a small confirmation page, and hands the request URL to the pure
// LoopbackRedirectParser. All branching logic worth testing lives in that parser; this class is the
// socket-accept boundary.
internal sealed class HttpListenerLoopbackOAuthListener : ILoopbackOAuthListener
{
    private const string ResponseBody =
        "<html><body><h2>FitbitSync</h2><p>Authorization received. You can close this window.</p></body></html>";

    public async Task<OAuthCallbackResult> WaitForCallbackAsync(Uri redirectUri, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(redirectUri);

        using var listener = new HttpListener();
        listener.Prefixes.Add(NormalizePrefix(redirectUri));
        listener.Start();

        try
        {
            using var registration = ct.Register(listener.Stop);
            var context = await listener.GetContextAsync().ConfigureAwait(false);

            await WriteConfirmationAsync(context.Response).ConfigureAwait(false);

            return LoopbackRedirectParser.Parse(context.Request.Url!);
        }
        finally
        {
            if (listener.IsListening)
            {
                listener.Stop();
            }
        }
    }

    private static string NormalizePrefix(Uri redirectUri)
    {
        var prefix = redirectUri.GetLeftPart(UriPartial.Path);
        return prefix.EndsWith('/') ? prefix : prefix + "/";
    }

    private static async Task WriteConfirmationAsync(HttpListenerResponse response)
    {
        var bytes = Encoding.UTF8.GetBytes(ResponseBody);
        response.ContentType = "text/html";
        response.ContentLength64 = bytes.Length;
        await response.OutputStream.WriteAsync(bytes).ConfigureAwait(false);
        response.OutputStream.Close();
    }
}
