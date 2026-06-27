using FitbitSync.Domain;

namespace FitbitSync.Host;

public sealed record CliOptions
{
    public DateOnly? From { get; init; }

    public DateOnly? To { get; init; }

    public MetricType? Metric { get; init; }

    public bool Coverage { get; init; }

    public LoginMode LoginMode { get; init; } = LoginMode.Interactive;

    public string? Redirect { get; init; }
}
