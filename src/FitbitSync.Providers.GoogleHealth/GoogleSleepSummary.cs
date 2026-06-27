using System.Text.Json.Serialization;

namespace FitbitSync.Providers.GoogleHealth;

// The sleep session summary. minutesAsleep is the total minutes asleep (int64-as-string).
internal sealed class GoogleSleepSummary
{
    [JsonPropertyName("minutesAsleep")]
    public string? MinutesAsleep { get; set; }

    [JsonPropertyName("minutesAwake")]
    public string? MinutesAwake { get; set; }

    [JsonPropertyName("minutesInSleepPeriod")]
    public string? MinutesInSleepPeriod { get; set; }
}
