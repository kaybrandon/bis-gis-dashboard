using System.Globalization;
using System.Text;
using GisDashboard.Application.TimeReports;

namespace GisDashboard.Infrastructure.Services;

public static class TimeReportCsv
{
    public static TimeReportFile Build(TimeReportResponse report)
    {
        var builder = new StringBuilder();
        builder.Append('\uFEFF');
        builder.AppendLine("Worked On,Logged By,Client,Work Item,Hours,Minutes,Duration,Note");
        foreach (var row in report.Entries)
        {
            builder.AppendLine(string.Join(',',
                Csv(row.WorkedOn.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture)),
                Csv(row.LoggedByName),
                Csv(row.OrganizationName),
                Csv(row.FileName),
                row.Hours.ToString(CultureInfo.InvariantCulture),
                row.Minutes,
                Csv(row.HoursLabel),
                Csv(row.Note ?? "")));
        }

        var name = $"gis-time-report-{report.From:yyyy-MM-dd}-{report.To:yyyy-MM-dd}.csv";
        return new TimeReportFile(Encoding.UTF8.GetBytes(builder.ToString()), name, "text/csv");
    }

    private static string Csv(string value)
    {
        var escaped = value.Replace("\"", "\"\"", StringComparison.Ordinal);
        return $"\"{escaped}\"";
    }
}
