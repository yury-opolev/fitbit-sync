using System.Net;
using System.Net.Http.Headers;

namespace FitbitSync.Providers.Fitbit;

public sealed class BearerTokenHandler : DelegatingHandler
{
    private const string Scheme = "Bearer";

    private readonly IAccessTokenSource accessTokenSource;

    public BearerTokenHandler(IAccessTokenSource accessTokenSource)
    {
        ArgumentNullException.ThrowIfNull(accessTokenSource);
        this.accessTokenSource = accessTokenSource;
    }

    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        var accessToken = await this.accessTokenSource.GetAccessTokenAsync(cancellationToken).ConfigureAwait(false);
        request.Headers.Authorization = new AuthenticationHeaderValue(Scheme, accessToken);

        var response = await base.SendAsync(request, cancellationToken).ConfigureAwait(false);

        if (response.StatusCode != HttpStatusCode.Unauthorized)
        {
            return response;
        }

        // Reactive refresh-on-401: force a coordinated refresh (single-flight collapses concurrent 401s),
        // then retry the request EXACTLY ONCE with the new token. All Fitbit data calls are GETs with no
        // body, so cloning method + URI is sufficient and safe to re-send.
        var refreshedToken = await this.accessTokenSource.RefreshAccessTokenAsync(accessToken, cancellationToken).ConfigureAwait(false);

        // If nothing actually changed, don't bother retrying — surface the original 401 response.
        if (refreshedToken == accessToken)
        {
            return response;
        }

        response.Dispose();

        using var retry = new HttpRequestMessage(request.Method, request.RequestUri);
        retry.Headers.Authorization = new AuthenticationHeaderValue(Scheme, refreshedToken);

        return await base.SendAsync(retry, cancellationToken).ConfigureAwait(false);
    }
}
