using FitbitSync.Domain;
using FluentAssertions;

namespace FitbitSync.Providers.GoogleHealth.Tests;

// dataType ids are the kebab-case of the DataPoint union field; the filter member uses the camelCase field
// plus the type's time path. Metrics without a confirmed listable Google mapping are not registered (Resolve
// throws), so the provider advertises only what actually works.
public sealed class GoogleHealthDataTypeCatalogTests
{
    [Fact]
    public void Resolve_Steps_UsesKebabCaseDataTypeAndIntervalCivilStartFilter()
    {
        var descriptor = GoogleHealthDataTypeCatalog.Resolve(MetricType.Steps);

        descriptor.DataType.Should().Be("steps");
        descriptor.FilterMember.Should().Be("steps.interval.civil_start_time");
    }

    [Theory]
    [InlineData(MetricType.HeartRate, "heart-rate", "heart_rate.sample_time.civil_time")]
    [InlineData(MetricType.Sleep, "sleep", "sleep.interval.civil_end_time")]
    [InlineData(MetricType.SpO2, "oxygen-saturation", "oxygen_saturation.sample_time.civil_time")]
    [InlineData(MetricType.Hrv, "heart-rate-variability", "heart_rate_variability.sample_time.civil_time")]
    [InlineData(MetricType.ActiveZoneMinutes, "active-zone-minutes", "active_zone_minutes.interval.civil_start_time")]
    [InlineData(MetricType.VO2Max, "vo2-max", "vo2_max.sample_time.civil_time")]
    public void Resolve_MultiWordMetric_UsesKebabCaseDataTypeAndSnakeCaseFilterMember(MetricType metric, string expectedDataType, string expectedFilterMember)
    {
        var descriptor = GoogleHealthDataTypeCatalog.Resolve(metric);

        descriptor.DataType.Should().Be(expectedDataType);
        descriptor.FilterMember.Should().Be(expectedFilterMember);
    }

    [Fact]
    public void Resolve_AllRegisteredMetrics_FilterMemberHasNoUppercase()
    {
        foreach (var metric in Enum.GetValues<MetricType>())
        {
            if (!GoogleHealthDataTypeCatalog.IsSupported(metric))
            {
                continue;
            }

            var filterMember = GoogleHealthDataTypeCatalog.Resolve(metric).FilterMember;
            filterMember.Should().NotMatchRegex("[A-Z]", $"Google's filter language rejects camelCase member segments (metric {metric})");
        }
    }

    [Fact]
    public void Resolve_UnmappedMetric_Throws()
    {
        var act = () => GoogleHealthDataTypeCatalog.Resolve(MetricType.Temperature);

        act.Should().Throw<NotSupportedException>();
    }

    [Fact]
    public void IsSupported_ReflectsRegistration()
    {
        GoogleHealthDataTypeCatalog.IsSupported(MetricType.Steps).Should().BeTrue();
        GoogleHealthDataTypeCatalog.IsSupported(MetricType.Temperature).Should().BeFalse();
    }
}
