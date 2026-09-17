using System.Globalization;
using System.Text;
using GisDashboard.Application.Reports;

namespace GisDashboard.Infrastructure.Reports;

public static class MaintenanceReportCsv
{
    public static ReportFile Build(ReportSnapshot snapshot)
    {
        var builder = new StringBuilder();
        builder.Append('\uFEFF');
        builder.AppendLine("File Name,Upload Date,Worked Date,Annexations,Corrections,Plats,Deeds,Sketch,Property Ids");
        foreach (var item in snapshot.CompletedItems)
        {
            builder.AppendLine(string.Join(',',
                Csv(item.FileName),
                Csv(item.UploadedAt.ToString("MMM dd, yyyy", CultureInfo.InvariantCulture)),
                Csv(item.WorkedOn?.ToString("MMM dd, yyyy", CultureInfo.InvariantCulture) ?? ""),
                item.Annexations,
                item.Corrections,
                item.Plats,
                item.Deeds,
                Csv(item.Sketch ? "Yes" : "No"),
                Csv(item.PropertyIds)));
        }

        return new ReportFile(
            Encoding.UTF8.GetBytes(builder.ToString()),
            $"{Sanitize(snapshot.OrganizationName)} {snapshot.MonthLabel} completed maintenance items.csv",
            "text/csv");
    }

    private static string Csv(string value)
    {
        var escaped = value.Replace("\"", "\"\"", StringComparison.Ordinal);
        return $"\"{escaped}\"";
    }

    private static string Sanitize(string value)
    {
        var chars = value.Select(c => Path.GetInvalidFileNameChars().Contains(c) ? '-' : c).ToArray();
        return new string(chars).Trim();
    }
}
