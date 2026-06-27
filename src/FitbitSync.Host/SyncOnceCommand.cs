using FitbitSync.Application;
using FitbitSync.Persistence;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace FitbitSync.Host;

// Thin agent shell: runs a SINGLE incremental sync pass (ISyncEngine.RunOnceAsync) and does NOT start the
// scheduler, then emits the JSON envelope and a meaningful exit code. All sync logic + outcomes are tested
// in the Application layer; this shell only initializes the schema, maps SyncRunResult -> envelope, and
// translates the outcome to an exit code (0 ok, 2 faulted, 3 rate-limited). Never prints secrets.
internal static class SyncOnceCommand
{
    public const string Name = "sync-once";

    public static async Task<int> ExecuteAsync(IHost host, CliOptions? options, CancellationToken ct = default)
    {
        using var scope = host.Services.CreateScope();
        var services = scope.ServiceProvider;

        services.GetRequiredService<ISchemaInitializer>().Initialize();

        var result = await services.GetRequiredService<ISyncEngine>().RunOnceAsync(ct).ConfigureAwait(false);
        var exitCode = AgentOutcome.ToExitCode(result.Outcome);

        var data = new
        {
            runId = result.RunId,
            itemsPlanned = result.ItemsPlanned,
            itemsCompleted = result.ItemsCompleted,
            samplesWritten = result.SamplesWritten,
            outcome = result.Outcome,
        };

        return AgentConsole.Emit(AgentResponse.ForResult(Name, exitCode, data));
    }
}
