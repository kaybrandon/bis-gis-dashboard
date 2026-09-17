using GisDashboard.Application.Exceptions;

namespace GisDashboard.Application.WorkItems;

public static class TimeDurations
{
    public const int MaxMinutes = 24 * 60;

    public static int ResolveMinutes(int? hours, int? minutes, decimal? decimalHours)
    {
        int total;
        if (hours is not null || minutes is not null)
        {
            var h = hours ?? 0;
            var m = minutes ?? 0;
            if (h is < 0 or > 24)
            {
                throw new ValidationException("Hours must be between 0 and 24.");
            }

            if (m is < 0 or > 59)
            {
                throw new ValidationException("Minutes must be between 0 and 59.");
            }

            total = (h * 60) + m;
        }
        else if (decimalHours is { } dec)
        {
            if (dec <= 0 || dec > 24)
            {
                throw new ValidationException("Decimal hours must be greater than 0 and at most 24.");
            }

            total = (int)Math.Round(dec * 60m, MidpointRounding.AwayFromZero);
        }
        else
        {
            throw new ValidationException("Enter hours and minutes, or decimal hours.");
        }

        if (total < 1)
        {
            throw new ValidationException("Log at least 1 minute.");
        }

        if (total > MaxMinutes)
        {
            throw new ValidationException("A single time entry cannot exceed 24 hours.");
        }

        return total;
    }

    public static string Format(int minutes)
    {
        var h = minutes / 60;
        var m = minutes % 60;
        if (h > 0 && m > 0)
        {
            return $"{h}h {m}m";
        }

        if (h > 0)
        {
            return $"{h}h";
        }

        return $"{m}m";
    }

    public static decimal ToHours(int minutes) => Math.Round(minutes / 60m, 2);
}
