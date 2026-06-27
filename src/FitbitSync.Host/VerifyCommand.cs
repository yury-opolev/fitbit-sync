using FitbitSync.Domain;
using FitbitSync.Persistence;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace FitbitSync.Host;

// Thin untested shell: opens the encrypted database, runs the IntegrityVerifier (audit hash-chain +
// per-row signature re-verification), prints the report, and exits 0 when intact / 2 when tampering is
// detected. All branching logic worth testing lives in IntegrityVerifier (Persistence integration tests).
internal static class VerifyCommand
{
    public static async Task<int> ExecuteAsync(IHost host, CancellationToken ct = default)
    {
        using var scope = host.Services.CreateScope();
        var services = scope.ServiceProvider;

        services.GetRequiredService<ISchemaInitializer>().Initialize();

        var report = await services.GetRequiredService<IIntegrityVerifier>().VerifyAsync(ct).ConfigureAwait(false);

        Console.WriteLine($"Audit chain intact:   {report.IsAuditChainIntact}");
        Console.WriteLine($"Samples verified:     {report.VerifiedSampleCount}");
        Console.WriteLine($"Samples forged:       {report.ForgedSampleCount}");

        if (report.IsValid)
        {
            Console.WriteLine("Integrity OK.");
            return 0;
        }

        Console.Error.WriteLine("Integrity verification FAILED: tampering detected.");
        return 2;
    }
}
