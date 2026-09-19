using System.Text;
using GisDashboard.Application.WorkItems;

namespace GisDashboard.Infrastructure.Export;

public static class WorkItemCsvExport
{
    public static ExcelExport Build(IReadOnlyList<WorkItemListItem> items, string contentType = "application/vnd.ms-excel")
    {
        var builder = new StringBuilder();
        builder.Append('\uFEFF');
        builder.AppendLine(string.Join(',', WorkItemExportColumns.Headers));
        foreach (var item in items)
        {
            builder.AppendLine(string.Join(',', WorkItemExportColumns.Values(item).Select(Csv)));
        }

        return new ExcelExport(
            Encoding.UTF8.GetBytes(builder.ToString()),
            "gis-work-items.csv",
            contentType);
    }

    internal static string Csv(string value)
    {
        var escaped = value.Replace("\"", "\"\"", StringComparison.Ordinal);
        return $"\"{escaped}\"";
    }
}
