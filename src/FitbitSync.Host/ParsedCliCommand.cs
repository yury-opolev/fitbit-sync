namespace FitbitSync.Host;

public sealed record ParsedCliCommand(CliVerb Verb, string? Error = null, CliOptions? Options = null)
{
    public bool IsValid => this.Error is null && this.Verb is not CliVerb.None;
}
