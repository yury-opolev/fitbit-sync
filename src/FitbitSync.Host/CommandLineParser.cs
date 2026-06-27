using FitbitSync.Domain;

namespace FitbitSync.Host;

public static class CommandLineParser
{
    public const string UsageText =
        "Usage: fitbitsync <command>\n" +
        "\n" +
        "Operator commands:\n" +
        "  login        Run the one-time loopback OAuth flow (desktop browser) and persist tokens.\n" +
        "  login --begin                       Headless step 1: emit the authorize URL as JSON and store the pending login.\n" +
        "  login --complete --redirect <url>   Headless step 2: finish login from the pasted callback URL (JSON).\n" +
        "  run          Start the host and the background sync scheduler.\n" +
        "  verify       Verify audit-chain and stored-sample integrity, then exit.\n" +
        "  rotate-keys  Roll the signing key and re-encrypt the database, then exit.\n" +
        "  help         Show this help text.\n" +
        "\n" +
        "Agent commands (emit a JSON envelope on stdout):\n" +
        "  sync-once                       Run a single incremental sync pass (no scheduler), then exit.\n" +
        "  backfill --from D --to D [--metric M]   Gap-aware historical fill; fetches only missing dates.\n" +
        "  query --metric M --from D --to D        Read stored samples as JSON (read-only).\n" +
        "  query --coverage [--metric M] [--from D --to D]   Report held date span + gaps per metric.\n" +
        "\n" +
        "  D is an ISO date (yyyy-MM-dd). M is a metric name (e.g. heartRate, sleep, spO2).";

    public static ParsedCliCommand Parse(IReadOnlyList<string> args)
    {
        ArgumentNullException.ThrowIfNull(args);

        if (args.Count == 0)
        {
            return new ParsedCliCommand(CliVerb.Help);
        }

        var verb = args[0].Trim();

        return verb.ToLowerInvariant() switch
        {
            "login" => ParseLogin(args),
            "run" => new ParsedCliCommand(CliVerb.Run),
            "verify" => new ParsedCliCommand(CliVerb.Verify),
            "rotate-keys" => new ParsedCliCommand(CliVerb.RotateKeys),
            "sync-once" => ParseOptions(CliVerb.SyncOnce, args),
            "backfill" => ParseOptions(CliVerb.Backfill, args),
            "query" => ParseOptions(CliVerb.Query, args),
            "help" or "--help" or "-h" => new ParsedCliCommand(CliVerb.Help),
            _ => new ParsedCliCommand(CliVerb.None, $"Unknown command '{verb}'."),
        };
    }

    private static ParsedCliCommand ParseLogin(IReadOnlyList<string> args)
    {
        var begin = false;
        var complete = false;
        string? redirect = null;

        for (var i = 1; i < args.Count; i++)
        {
            var token = args[i].Trim();

            switch (token.ToLowerInvariant())
            {
                case "--begin":
                    begin = true;
                    break;

                case "--complete":
                    complete = true;
                    break;

                case "--redirect":
                    if (!TryTakeValue(args, ref i, out var redirectValue))
                    {
                        return InvalidLogin(begin, complete, "login --redirect requires a value (the full callback URL).");
                    }

                    redirect = redirectValue;
                    break;

                default:
                    return InvalidLogin(begin, complete, $"login: unknown option '{token}'.");
            }
        }

        if (begin && complete)
        {
            return InvalidLogin(begin, complete, "login: --begin and --complete are mutually exclusive.");
        }

        if (complete)
        {
            if (redirect is null)
            {
                return InvalidLogin(begin, complete, "login --complete requires --redirect <callback-url>.");
            }

            return new ParsedCliCommand(CliVerb.Login, Options: new CliOptions { LoginMode = LoginMode.Complete, Redirect = redirect });
        }

        if (begin)
        {
            return new ParsedCliCommand(CliVerb.Login, Options: new CliOptions { LoginMode = LoginMode.Begin });
        }

        if (redirect is not null)
        {
            return new ParsedCliCommand(
                CliVerb.Login,
                "login: --redirect is only valid together with --complete.",
                new CliOptions { LoginMode = LoginMode.Interactive });
        }

        return new ParsedCliCommand(CliVerb.Login, Options: new CliOptions { LoginMode = LoginMode.Interactive });
    }

    private static ParsedCliCommand InvalidLogin(bool begin, bool complete, string message)
    {
        var mode = complete ? LoginMode.Complete : begin ? LoginMode.Begin : LoginMode.Interactive;
        return new ParsedCliCommand(CliVerb.Login, message, new CliOptions { LoginMode = mode });
    }

    private static ParsedCliCommand ParseOptions(CliVerb verb, IReadOnlyList<string> args)
    {
        DateOnly? from = null;
        DateOnly? to = null;
        MetricType? metric = null;
        var coverage = false;

        for (var i = 1; i < args.Count; i++)
        {
            var token = args[i].Trim();

            switch (token.ToLowerInvariant())
            {
                case "--coverage":
                    coverage = true;
                    break;

                case "--from":
                    if (!TryTakeValue(args, ref i, out var fromValue))
                    {
                        return Invalid(verb, "--from requires a value (yyyy-MM-dd).");
                    }

                    if (!TryParseDate(fromValue, out var fromDate))
                    {
                        return Invalid(verb, $"--from '{fromValue}' is not a valid ISO date (yyyy-MM-dd).");
                    }

                    from = fromDate;
                    break;

                case "--to":
                    if (!TryTakeValue(args, ref i, out var toValue))
                    {
                        return Invalid(verb, "--to requires a value (yyyy-MM-dd).");
                    }

                    if (!TryParseDate(toValue, out var toDate))
                    {
                        return Invalid(verb, $"--to '{toValue}' is not a valid ISO date (yyyy-MM-dd).");
                    }

                    to = toDate;
                    break;

                case "--metric":
                    if (!TryTakeValue(args, ref i, out var metricValue))
                    {
                        return Invalid(verb, "--metric requires a value.");
                    }

                    if (!TryParseMetric(metricValue, out var parsedMetric))
                    {
                        return Invalid(verb, $"--metric '{metricValue}' is not a known metric type.");
                    }

                    metric = parsedMetric;
                    break;

                default:
                    return Invalid(verb, $"Unknown option '{token}'.");
            }
        }

        return new ParsedCliCommand(verb, Options: new CliOptions
        {
            From = from,
            To = to,
            Metric = metric,
            Coverage = coverage,
        });
    }

    private static bool TryTakeValue(IReadOnlyList<string> args, ref int index, out string value)
    {
        if (index + 1 >= args.Count)
        {
            value = "";
            return false;
        }

        index++;
        value = args[index].Trim();
        return !string.IsNullOrEmpty(value) && !value.StartsWith("--", StringComparison.Ordinal);
    }

    private static bool TryParseDate(string value, out DateOnly date) =>
        DateOnly.TryParseExact(value, "yyyy-MM-dd", out date);

    private static bool TryParseMetric(string value, out MetricType metric) =>
        Enum.TryParse(value, ignoreCase: true, out metric) && Enum.IsDefined(metric);

    private static ParsedCliCommand Invalid(CliVerb verb, string message) =>
        new(verb, message);
}
