using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace FitbitSync.Host;

// Thin untested shell: builds the Generic Host. Configuration layers (most to least specific): explicit
// environment variables and User Secrets over appsettings.json. Secrets (Fitbit client id/secret, the DB
// passphrase, and the column/signing keys) live in User Secrets during development and environment
// variables at runtime — never in source. In agentMode, all framework logging is routed to STDERR so the
// agent verbs can emit exactly one JSON envelope on stdout with nothing else polluting it.
internal static class FitbitSyncHostFactory
{
    public static IHost Create(string[] args, bool agentMode = false)
    {
        var builder = Microsoft.Extensions.Hosting.Host.CreateApplicationBuilder(args);

        builder.Configuration.AddUserSecrets(typeof(FitbitSyncHostFactory).Assembly, optional: true);
        builder.Configuration.AddEnvironmentVariables();

        if (agentMode)
        {
            builder.Logging.ClearProviders();
            builder.Logging.AddConsole(options => options.LogToStandardErrorThreshold = LogLevel.Trace);
        }

        builder.Services.AddFitbitSyncHost(builder.Configuration);

        return builder.Build();
    }
}
