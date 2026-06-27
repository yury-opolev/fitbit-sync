using FitbitSync.Domain;

namespace FitbitSync.Host;

public static class AgentArguments
{
    public static DateRange RequireRange(CliOptions? options)
    {
        if (options?.From is not { } from)
        {
            throw new AgentCommandException("usage", "--from is required (yyyy-MM-dd).");
        }

        if (options.To is not { } to)
        {
            throw new AgentCommandException("usage", "--to is required (yyyy-MM-dd).");
        }

        if (from > to)
        {
            throw new AgentCommandException("usage", $"--from ({from:yyyy-MM-dd}) must be on or before --to ({to:yyyy-MM-dd}).");
        }

        return new DateRange(from, to);
    }

    public static MetricType RequireMetric(CliOptions? options)
    {
        if (options?.Metric is not { } metric)
        {
            throw new AgentCommandException("usage", "--metric is required.");
        }

        return metric;
    }
}
