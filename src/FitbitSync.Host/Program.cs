using Microsoft.Extensions.Hosting;

namespace FitbitSync.Host;

internal static class Program
{
    private static async Task<int> Main(string[] args)
    {
        var command = CommandLineParser.Parse(args);

        switch (command.Verb)
        {
            case CliVerb.Help:
                Console.WriteLine(CommandLineParser.UsageText);
                return 0;

            case CliVerb.SyncOnce:
                return await RunAgentCommandAsync(args, command, SyncOnceCommand.Name,
                    (host, options, ct) => SyncOnceCommand.ExecuteAsync(host, options, ct)).ConfigureAwait(false);

            case CliVerb.Backfill:
                return await RunAgentCommandAsync(args, command, BackfillCommandShell.Name,
                    (host, options, ct) => BackfillCommandShell.ExecuteAsync(host, options, ct)).ConfigureAwait(false);

            case CliVerb.Query:
                return await RunAgentCommandAsync(args, command, QueryCommandShell.Name,
                    (host, options, ct) => QueryCommandShell.ExecuteAsync(host, options, ct)).ConfigureAwait(false);

            case CliVerb.Login when command.Options?.LoginMode == LoginMode.Begin:
                return await RunAgentCommandAsync(args, command, BeginLoginCommand.Name,
                    (host, options, ct) => BeginLoginCommand.ExecuteAsync(host, options, ct)).ConfigureAwait(false);

            case CliVerb.Login when command.Options?.LoginMode == LoginMode.Complete:
                return await RunAgentCommandAsync(args, command, CompleteLoginCommand.Name,
                    (host, options, ct) => CompleteLoginCommand.ExecuteAsync(host, options, ct)).ConfigureAwait(false);
        }

        if (command.Error is not null)
        {
            Console.Error.WriteLine(command.Error);
            Console.Error.WriteLine(CommandLineParser.UsageText);
            return 1;
        }

        switch (command.Verb)
        {
            case CliVerb.Login:
                return await RunHostCommandAsync(args, LoginCommand.ExecuteAsync).ConfigureAwait(false);

            case CliVerb.Run:
                return await RunHostCommandAsync(args, RunCommand.ExecuteAsync).ConfigureAwait(false);

            case CliVerb.Verify:
                return await RunHostCommandAsync(args, VerifyCommand.ExecuteAsync).ConfigureAwait(false);

            case CliVerb.RotateKeys:
                return await RunHostCommandAsync(args, RotateKeysCommand.ExecuteAsync).ConfigureAwait(false);

            default:
                Console.Error.WriteLine(CommandLineParser.UsageText);
                return 1;
        }
    }

    private static async Task<int> RunHostCommandAsync(string[] args, Func<IHost, CancellationToken, Task<int>> execute)
    {
        try
        {
            using var host = FitbitSyncHostFactory.Create(args);
            return await execute(host, CancellationToken.None).ConfigureAwait(false);
        }
        catch (Exception ex) when (ex is InvalidOperationException or ArgumentException)
        {
            Console.Error.WriteLine($"Startup failed: {ex.Message}");
            Console.Error.WriteLine("Check your configuration (User Secrets in development, environment variables at runtime).");
            return 1;
        }
    }

    private static async Task<int> RunAgentCommandAsync(
        string[] args,
        ParsedCliCommand command,
        string commandName,
        Func<IHost, CliOptions?, CancellationToken, Task<int>> execute)
    {
        if (command.Error is not null)
        {
            return AgentConsole.Emit(AgentResponse.Failure(commandName, AgentExitCode.UsageOrConfigFailure, "usage", command.Error));
        }

        try
        {
            using var host = FitbitSyncHostFactory.Create(args, agentMode: true);
            return await execute(host, command.Options, CancellationToken.None).ConfigureAwait(false);
        }
        catch (AgentCommandException ex)
        {
            return AgentConsole.Emit(AgentResponse.Failure(commandName, AgentExitCode.UsageOrConfigFailure, ex.Code, ex.Message));
        }
        catch (Exception ex) when (ex is InvalidOperationException or ArgumentException)
        {
            return AgentConsole.Emit(AgentResponse.Failure(commandName, AgentExitCode.UsageOrConfigFailure, "startup", ex.Message));
        }
    }
}
