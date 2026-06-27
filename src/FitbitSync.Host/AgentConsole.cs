namespace FitbitSync.Host;

internal static class AgentConsole
{
    public static int Emit(AgentResponse response)
    {
        Console.WriteLine(AgentJson.Serialize(response));
        return response.ExitCode;
    }
}
