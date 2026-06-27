using FitbitSync.Persistence;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace FitbitSync.Host;

// Thin untested shell: initializes the schema, then runs the host (and its SyncScheduler BackgroundService)
// until the process is stopped (Ctrl+C / SIGTERM).
internal static class RunCommand
{
    public static async Task<int> ExecuteAsync(IHost host, CancellationToken ct = default)
    {
        using (var scope = host.Services.CreateScope())
        {
            scope.ServiceProvider.GetRequiredService<ISchemaInitializer>().Initialize();
        }

        Console.WriteLine("FitbitSync host started. Press Ctrl+C to stop.");
        await host.RunAsync(ct).ConfigureAwait(false);
        return 0;
    }
}
