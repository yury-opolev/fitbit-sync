using FitbitSync.Application;
using FitbitSync.Domain;
using FitbitSync.Persistence;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace FitbitSync.Host;

// Thin agent shell: READ-ONLY. Two modes, never triggers a sync.
//   query --coverage [--metric M] [--from D --to D]  -> per-metric held date span + interior gaps.
//   query --metric M --from D --to D                 -> the stored samples in range.
// Empty results are NOT an error: the envelope is ok=true, exit 0, with an empty list. The coverage shape
// and gap detection reuse the tested CoverageGapCalculator + IMetricRepository read path. Never prints
// secrets — only metric types, dates, counts, and sample values/units.
internal static class QueryCommandShell
{
    public const string Name = "query";

    public static async Task<int> ExecuteAsync(IHost host, CliOptions? options, CancellationToken ct = default)
    {
        using var scope = host.Services.CreateScope();
        var services = scope.ServiceProvider;

        services.GetRequiredService<ISchemaInitializer>().Initialize();

        var repository = services.GetRequiredService<IMetricRepository>();

        if (options?.Coverage == true)
        {
            var capabilities = services.GetRequiredService<IHealthDataProvider>().Capabilities;
            return await EmitCoverageAsync(repository, capabilities, options, ct).ConfigureAwait(false);
        }

        return await EmitSamplesAsync(repository, options, ct).ConfigureAwait(false);
    }

    private static async Task<int> EmitSamplesAsync(IMetricRepository repository, CliOptions? options, CancellationToken ct)
    {
        var metric = AgentArguments.RequireMetric(options);
        var range = AgentArguments.RequireRange(options);

        var samples = await repository.QueryAsync(metric, range, ct).ConfigureAwait(false);

        var data = new
        {
            metric,
            range = new { from = range.Start, to = range.End },
            count = samples.Count,
            samples = samples.Select(sample => new
            {
                timestamp = sample.Timestamp,
                value = sample.Value,
                unit = sample.Unit,
                resolution = sample.Resolution,
            }),
        };

        return AgentConsole.Emit(AgentResponse.Success(Name, AgentExitCode.Success, data));
    }

    private static async Task<int> EmitCoverageAsync(
        IMetricRepository repository,
        IReadOnlyList<MetricCapability> capabilities,
        CliOptions options,
        CancellationToken ct)
    {
        var metrics = options.Metric is { } single
            ? [single]
            : capabilities.Select(capability => capability.Metric).Distinct().ToList();

        var coverage = new List<object>();

        foreach (var metric in metrics)
        {
            var samples = await repository.QueryAsync(metric, FullRangeFor(options), ct).ConfigureAwait(false);
            var present = samples.Select(sample => DateOnly.FromDateTime(sample.Timestamp.UtcDateTime)).ToList();
            var range = options.From is { } from && options.To is { } to
                ? new DateRange(from, to)
                : RangeOf(present);

            if (range is null)
            {
                coverage.Add(new { metric, heldFrom = (DateOnly?)null, heldTo = (DateOnly?)null, daysHeld = 0, gapCount = 0, gaps = Array.Empty<string>() });
                continue;
            }

            var report = CoverageGapCalculator.CoverageOf(metric, present, range);
            coverage.Add(new
            {
                metric,
                heldFrom = report.HeldFrom,
                heldTo = report.HeldTo,
                daysHeld = report.DaysHeld,
                gapCount = report.Gaps.Count,
                gaps = report.Gaps.Select(date => date.ToString("yyyy-MM-dd")),
            });
        }

        return AgentConsole.Emit(AgentResponse.Success(Name, AgentExitCode.Success, new { coverage }));
    }

    private static DateRange FullRangeFor(CliOptions options) =>
        options.From is { } from && options.To is { } to
            ? new DateRange(from, to)
            : new DateRange(DateOnly.MinValue, DateOnly.MaxValue);

    private static DateRange? RangeOf(IReadOnlyList<DateOnly> present) =>
        present.Count == 0 ? null : new DateRange(present.Min(), present.Max());
}
