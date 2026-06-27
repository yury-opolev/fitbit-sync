using System.Globalization;
using FitbitSync.Domain;

namespace FitbitSync.Providers.Fitbit;

public sealed class RateLimitHandler : DelegatingHandler
{
    private const string LimitHeader = "Fitbit-Rate-Limit-Limit";
    private const string RemainingHeader = "Fitbit-Rate-Limit-Remaining";
    private const string ResetHeader = "Fitbit-Rate-Limit-Reset";

    private readonly RateLimitSnapshotHolder holder;
    private readonly IClock clock;

    public RateLimitHandler(RateLimitSnapshotHolder holder, IClock clock)
    {
        ArgumentNullException.ThrowIfNull(holder);
        ArgumentNullException.ThrowIfNull(clock);

        this.holder = holder;
        this.clock = clock;
    }

    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        var response = await base.SendAsync(request, cancellationToken).ConfigureAwait(false);

        if (TryReadHeader(response, LimitHeader, out var limit)
            && TryReadHeader(response, RemainingHeader, out var remaining)
            && TryReadHeader(response, ResetHeader, out var resetSeconds))
        {
            this.holder.Latest = new RateLimitSnapshot(remaining, limit, resetSeconds, this.clock.UtcNow);
        }

        return response;
    }

    private static bool TryReadHeader(HttpResponseMessage response, string name, out int value)
    {
        value = 0;

        if (response.Headers.TryGetValues(name, out var values))
        {
            var raw = values.FirstOrDefault();
            return raw is not null && int.TryParse(raw, NumberStyles.Integer, CultureInfo.InvariantCulture, out value);
        }

        return false;
    }
}
