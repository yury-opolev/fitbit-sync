namespace FitbitSync.Domain;

public static class CoverageGapCalculator
{
    public static IReadOnlyList<DateOnly> MissingDates(IEnumerable<DateOnly> presentDates, DateRange range)
    {
        ArgumentNullException.ThrowIfNull(presentDates);
        ArgumentNullException.ThrowIfNull(range);

        var present = presentDates as IReadOnlySet<DateOnly> ?? presentDates.ToHashSet();
        var missing = new List<DateOnly>();

        for (var date = range.Start; date <= range.End; date = date.AddDays(1))
        {
            if (!present.Contains(date))
            {
                missing.Add(date);
            }
        }

        return missing;
    }

    public static MetricCoverage CoverageOf(MetricType metric, IEnumerable<DateOnly> presentDates, DateRange range)
    {
        ArgumentNullException.ThrowIfNull(presentDates);
        ArgumentNullException.ThrowIfNull(range);

        var present = presentDates as IReadOnlySet<DateOnly> ?? presentDates.ToHashSet();

        DateOnly? heldFrom = null;
        DateOnly? heldTo = null;
        var daysHeld = 0;
        var gaps = new List<DateOnly>();

        for (var date = range.Start; date <= range.End; date = date.AddDays(1))
        {
            if (present.Contains(date))
            {
                heldFrom ??= date;
                heldTo = date;
                daysHeld++;
            }
            else
            {
                gaps.Add(date);
            }
        }

        var interiorGaps = TrimToInterior(gaps, heldFrom, heldTo);
        return new MetricCoverage(metric, range, heldFrom, heldTo, daysHeld, interiorGaps);
    }

    private static IReadOnlyList<DateOnly> TrimToInterior(List<DateOnly> gaps, DateOnly? heldFrom, DateOnly? heldTo)
    {
        if (heldFrom is not { } from || heldTo is not { } to)
        {
            return gaps;
        }

        return gaps.Where(date => date >= from && date <= to).ToList();
    }
}
