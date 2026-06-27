using FitbitSync.Domain;
using FitbitSync.Providers.Fitbit;
using FluentAssertions;

namespace FitbitSync.Providers.Fitbit.Tests;

// Phase 3 first red test (updated in Phase 5b): the Fitbit adapter must advertise exactly the metrics
// we ship — HeartRate, Sleep, SpO2, BreathingRate, Hrv, Temperature, VO2Max, ActiveZoneMinutes, Steps.
// Sleep was deferred in Phase 3 and delivered in Phase 5, so it is now part of the advertised set.
// Capabilities drive generic backfill planning, so this contract is asserted before fetch/mapping logic.
public sealed class FitbitProviderCapabilitiesTests
{
    [Fact]
    public void FitbitProvider_AdvertisesCapabilities()
    {
        var (provider, _) = ProviderTestHarness.Build("https://api.fitbit.com");

        provider.Source.Should().Be("fitbit");

        var advertised = provider.Capabilities.Select(capability => capability.Metric).ToList();

        advertised.Should().Contain(
        [
            MetricType.HeartRate,
            MetricType.Sleep,
            MetricType.SpO2,
            MetricType.BreathingRate,
            MetricType.Hrv,
            MetricType.Temperature,
            MetricType.VO2Max,
            MetricType.ActiveZoneMinutes,
            MetricType.Steps,
        ]);
    }
}
