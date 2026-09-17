namespace GisDashboard.Application.WorkItems;

public static class DashboardRange
{
    public const int DefaultDays = 30;
    public const int MaxDays = 366;

    public static Resolved Resolve(DateTimeOffset? from, DateTimeOffset? to)
    {
        var today = DateTime.UtcNow.Date;
        var endDate = (to ?? new DateTimeOffset(today, TimeSpan.Zero)).UtcDateTime.Date;
        var startDate = from.HasValue
            ? from.Value.UtcDateTime.Date
            : endDate.AddDays(-(DefaultDays - 1));

        if (startDate > endDate)
        {
            (startDate, endDate) = (endDate, startDate);
        }

        if ((endDate - startDate).TotalDays > MaxDays - 1)
        {
            startDate = endDate.AddDays(-(MaxDays - 1));
        }

        var start = new DateTimeOffset(startDate, TimeSpan.Zero);
        var endExclusive = new DateTimeOffset(endDate.AddDays(1), TimeSpan.Zero);
        return new Resolved(start, endExclusive.AddTicks(-1), startDate, endDate, Label(startDate, endDate));
    }

    public static string Label(DateTime start, DateTime end)
    {
        if (start == end)
        {
            return start.ToString("MMM d, yyyy");
        }

        return start.Year == end.Year
            ? $"{start:MMM d} – {end:MMM d, yyyy}"
            : $"{start:MMM d, yyyy} – {end:MMM d, yyyy}";
    }

    public sealed record Resolved(
        DateTimeOffset From,
        DateTimeOffset To,
        DateTime StartDate,
        DateTime EndDate,
        string Label)
    {
        public long FromSort => From.ToUnixTimeMilliseconds();
        public long ToSort => To.ToUnixTimeMilliseconds();
        public int DayCount => (EndDate - StartDate).Days + 1;
    }
}
