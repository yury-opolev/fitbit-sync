namespace FitbitSync.Host;

public sealed class AgentCommandException : Exception
{
    public AgentCommandException(string code, string message)
        : base(message)
    {
        this.Code = code;
    }

    public string Code { get; }
}
