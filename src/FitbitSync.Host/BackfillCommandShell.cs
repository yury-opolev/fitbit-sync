using FitbitSync.Application;
using FitbitSync.Domain;
using FitbitSync.Persistence;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace FitbitSync.Host;

// Thin agent shell: validates --from/--to (fail-fast JSON usage error if missing, unparseable, or from>to)
// and the optional --metric filter, then delegates to the gap-aware ISyncEngine.RunBackfillAsync, which
// fetches ONLY the dates not already held (covered dates cost zero Fitbit calls). Emits the coverage delta
// (alreadyCovered / fetched / stillMissing per metric) as the JSON envelope. Gap logic + zero-call behavior
// are tested in the Application layer; this shell only maps the result. Never prints secrets.
internal static class BackfillCommandShell
{
    public const string Name = "backfill";

    public static async Task<int> ExecuteAsync(IHost host, CliOptions? options, CancellationToken ct = default)
    {
        var range = AgentArguments.RequireRange(options);
        IReadOnlyList<MetricType>? metrics = options?.Metric is { } metric ? [metric] : null;

        using var scope = host.Services.CreateScope();
        var services = scope.ServiceProvider;

        services.GetRequiredService<ISchemaInitializer>().Initialize();

        var result = await services.GetRequiredService<ISyncEngine>()
            .RunBackfillAsync(new BackfillCommand(Guid.NewGuid(), metrics, range), ct)
            .ConfigureAwait(false);

        var data = new
        {
            runId = result.RunId,
            requestedRange = new { from = range.Start, to = range.End },
            metrics = result.Metrics.Select(report => new
            {
                metric = report.Metric,
                alreadyCovered = Describe(report.AlreadyCovered),
                fetched = Describe(report.Fetched),
                stillMissing = Describe(report.StillMissing),
                samplesWritten = report.SamplesWritten,
            }),
            outcome = result.Outcome,
        };

        var exitCode = AgentOutcome.ToExitCode(result.Outcome);
        return AgentConsole.Emit(AgentResponse.ForResult(Name, exitCode, data));
    }

    private static object Describe(IReadOnlyList<DateOnly> dates) =>
        new { count = dates.Count, dates = dates.Select(date => date.ToString("yyyy-MM-dd")) };
}
