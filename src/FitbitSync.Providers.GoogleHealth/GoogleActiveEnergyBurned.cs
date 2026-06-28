using System.Text.Json.Serialization;

namespace FitbitSync.Providers.GoogleHealth;

// The "active-energy-burned" interval payload: energy burned during activity (excluding BMR), in
// kilocalories. Unlike count-style metrics, kcal arrives as a JSON number (double), not a string.
internal sealed class GoogleActiveEnergyBurned
{
    [JsonPropertyName("interval")]
    public GoogleObservationTimeInterval? Interval { get; set; }

    [JsonPropertyName("kcal")]
    public double? Kcal { get; set; }
}
