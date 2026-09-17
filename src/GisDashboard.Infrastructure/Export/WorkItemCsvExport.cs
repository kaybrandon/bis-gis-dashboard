using System.Text;
using GisDashboard.Application.WorkItems;

namespace GisDashboard.Infrastructure.Export;

public static class WorkItemCsvExport
{
    public static ExcelExport Build(IReadOnlyList<WorkItemListItem> items)
    {
        var builder = new StringBuilder();
        builder.Append('\uFEFF');
        builder.AppendLine("FileName,Client Name,Status,Assigned To,Upload Date,Worked Date,Hours,Priority,Needed By");
        foreach (var item in items)
        {
            builder.AppendLine(string.Join(',',
                Csv(item.FileName),
                Csv(item.OrganizationName),
                Csv(item.StatusName),
                Csv(item.AssignedToName ?? string.Empty),
                Csv(item.UploadedAt.ToString("yyyy-MM-dd HH:mm")),
                Csv(item.WorkedOn?.ToString("yyyy-MM-dd") ?? string.Empty),
                Csv(item.HoursLabel),
                Csv(item.IsPriority ? "Yes" : "No"),
                Csv(item.PriorityNeededBy?.ToString("yyyy-MM-dd") ?? string.Empty)));
        }

        return new ExcelExport(
            Encoding.UTF8.GetBytes(builder.ToString()),
            "gis-work-items.csv",
            "application/vnd.ms-excel");
    }

    private static string Csv(string value)
    {
        var escaped = value.Replace("\"", "\"\"", StringComparison.Ordinal);
        return $"\"{escaped}\"";
    }
}
