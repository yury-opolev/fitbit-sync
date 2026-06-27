namespace FitbitSync.Domain;

public sealed record DateRange
{
    public DateRange(DateOnly start, DateOnly end)
    {
        if (start > end)
        {
            throw new ArgumentOutOfRangeException(nameof(start), "Range start must be on or before end.");
        }

        this.Start = start;
        this.End = end;
    }

    public DateOnly Start { get; }

    public DateOnly End { get; }

    public static DateRange SingleDay(DateOnly date) => new(date, date);
}
