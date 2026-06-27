using System.Text.Json;
using System.Text.Json.Serialization;

namespace FitbitSync.Host;

public static class AgentJson
{
    public static readonly JsonSerializerOptions Options = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        WriteIndented = true,
        Converters = { new JsonStringEnumConverter(JsonNamingPolicy.CamelCase) },
    };

    public static string Serialize(AgentResponse response) => JsonSerializer.Serialize(response, Options);
}
