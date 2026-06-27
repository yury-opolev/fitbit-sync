namespace FitbitSync.Domain.Tests;

// Phase 8: CoverageGapCalculator is the single, pure gap engine shared by gap-aware backfill (fetch only
// MissingDates) and the coverage view (CoverageOf -> held span + interior gaps). These tests pin every
// edge case called out in the scope refinement: empty store (all dates missing), full coverage (no-op),
// interior gap, single-day range, boundary from==to, ordering, and the inverted-range rejection that the
// DateRange guard provides upstream.
public sealed class CoverageGapCalculatorTests
{
    private static readonly DateRange Jan1To5 = new(new DateOnly(2024, 1, 1), new DateOnly(2024, 1, 5));

    [Fact]
    public void MissingDates_EmptyStore_ReturnsEveryDateInRange()
    {
        var missing = CoverageGapCalculator.MissingDates([], Jan1To5);

        missing.Should().Equal(
            new DateOnly(2024, 1, 1),
            new DateOnly(2024, 1, 2),
            new DateOnly(2024, 1, 3),
            new DateOnly(2024, 1, 4),
            new DateOnly(2024, 1, 5));
    }

    [Fact]
    public void MissingDates_FullCoverage_ReturnsEmpty()
    {
        DateOnly[] present =
        [
            new(2024, 1, 1), new(2024, 1, 2), new(2024, 1, 3), new(2024, 1, 4), new(2024, 1, 5),
        ];

        var missing = CoverageGapCalculator.MissingDates(present, Jan1To5);

        missing.Should().BeEmpty();
    }

    [Fact]
    public void MissingDates_InteriorGap_ReturnsOnlyTheGapDates_Ordered()
    {
        // Hold the endpoints but not the middle — the interior gap must be detected, not masked.
        DateOnly[] present = [new(2024, 1, 1), new(2024, 1, 2), new(2024, 1, 5)];

        var missing = CoverageGapCalculator.MissingDates(present, Jan1To5);

        missing.Should().Equal(new DateOnly(2024, 1, 3), new DateOnly(2024, 1, 4));
    }

    [Fact]
    public void MissingDates_IgnoresPresentDatesOutsideRange()
    {
        DateOnly[] present = [new(2023, 12, 31), new(2024, 1, 3), new(2024, 2, 1)];

        var missing = CoverageGapCalculator.MissingDates(present, Jan1To5);

        missing.Should().Equal(
            new DateOnly(2024, 1, 1),
            new DateOnly(2024, 1, 2),
            new DateOnly(2024, 1, 4),
            new DateOnly(2024, 1, 5));
    }

    [Fact]
    public void MissingDates_SingleDayRange_Present_ReturnsEmpty()
    {
        var range = DateRange.SingleDay(new DateOnly(2024, 1, 3));

        CoverageGapCalculator.MissingDates([new DateOnly(2024, 1, 3)], range).Should().BeEmpty();
    }

    [Fact]
    public void MissingDates_SingleDayRange_Absent_ReturnsThatDay()
    {
        var range = DateRange.SingleDay(new DateOnly(2024, 1, 3));

        CoverageGapCalculator.MissingDates([], range).Should().Equal(new DateOnly(2024, 1, 3));
    }

    [Fact]
    public void DateRange_RejectsInvertedRange()
    {
        // The calculator trusts its DateRange input; the inverted-range rejection lives in the guard.
        var act = () => new DateRange(new DateOnly(2024, 1, 5), new DateOnly(2024, 1, 1));

        act.Should().Throw<ArgumentOutOfRangeException>();
    }

    [Fact]
    public void CoverageOf_EmptyStore_HasNoHeldSpan_AndAllGaps()
    {
        var coverage = CoverageGapCalculator.CoverageOf(MetricType.HeartRate, [], Jan1To5);

        coverage.HeldFrom.Should().BeNull();
        coverage.HeldTo.Should().BeNull();
        coverage.DaysHeld.Should().Be(0);
        coverage.Gaps.Should().HaveCount(5);
    }

    [Fact]
    public void CoverageOf_FullCoverage_ReportsSpan_AndNoGaps()
    {
        DateOnly[] present =
        [
            new(2024, 1, 1), new(2024, 1, 2), new(2024, 1, 3), new(2024, 1, 4), new(2024, 1, 5),
        ];

        var coverage = CoverageGapCalculator.CoverageOf(MetricType.HeartRate, present, Jan1To5);

        coverage.HeldFrom.Should().Be(new DateOnly(2024, 1, 1));
        coverage.HeldTo.Should().Be(new DateOnly(2024, 1, 5));
        coverage.DaysHeld.Should().Be(5);
        coverage.Gaps.Should().BeEmpty();
    }

    [Fact]
    public void CoverageOf_InteriorGap_ReportsHeldSpan_AndOnlyInteriorGaps()
    {
        // Leading/trailing absences are NOT gaps — only holes WITHIN the held span are reported.
        DateOnly[] present = [new(2024, 1, 2), new(2024, 1, 4)];

        var coverage = CoverageGapCalculator.CoverageOf(MetricType.SpO2, present, Jan1To5);

        coverage.HeldFrom.Should().Be(new DateOnly(2024, 1, 2));
        coverage.HeldTo.Should().Be(new DateOnly(2024, 1, 4));
        coverage.DaysHeld.Should().Be(2);
        coverage.Gaps.Should().Equal(new DateOnly(2024, 1, 3));
    }

    [Fact]
    public void CoverageOf_BoundaryFromEqualsTo_Present_HasSingleDaySpan()
    {
        var range = DateRange.SingleDay(new DateOnly(2024, 1, 3));

        var coverage = CoverageGapCalculator.CoverageOf(MetricType.Sleep, [new DateOnly(2024, 1, 3)], range);

        coverage.HeldFrom.Should().Be(new DateOnly(2024, 1, 3));
        coverage.HeldTo.Should().Be(new DateOnly(2024, 1, 3));
        coverage.DaysHeld.Should().Be(1);
        coverage.Gaps.Should().BeEmpty();
    }
}
