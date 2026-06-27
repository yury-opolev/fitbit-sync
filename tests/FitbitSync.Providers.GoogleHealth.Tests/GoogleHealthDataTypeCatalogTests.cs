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
