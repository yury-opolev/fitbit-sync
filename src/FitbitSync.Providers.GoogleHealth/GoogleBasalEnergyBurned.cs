using System.Text.Json.Serialization;

namespace FitbitSync.Providers.GoogleHealth;

// The "basal-energy-burned" interval payload: energy burned due to basal metabolic rate (BMR) over the
// observed interval, in kilocalories. kcal arrives as a JSON number (double).
internal sealed class GoogleBasalEnergyBurned
{
    [JsonPropertyName("interval")]
    public GoogleObservationTimeInterval? Interval { get; set; }

    [JsonPropertyName("kcal")]
    public double? Kcal { get; set; }
}
