using GisDashboard.Application.WorkItems;
using GisDashboard.Domain;

namespace GisDashboard.Infrastructure.Export;

public static class WorkItemExportColumns
{
    public static readonly string[] Headers =
    [
        "FileName",
        "Client Name",
        "Status",
        "Assigned To",
        "Upload Date",
        "Worked Date",
        "Hours",
        "Priority",
        "Needed By",
        DeedPlatFields.Survey,
        DeedPlatFields.Abstract,
        DeedPlatFields.LotBlock,
        DeedPlatFields.Subdivision,
        DeedPlatFields.LegalDescription
    ];

    public static string[] Values(WorkItemListItem item) =>
    [
        item.FileName,
        item.OrganizationName,
        item.StatusName,
        item.AssignedToName ?? string.Empty,
        item.UploadedAt.ToString("yyyy-MM-dd HH:mm"),
        item.WorkedOn?.ToString("yyyy-MM-dd") ?? string.Empty,
        item.HoursLabel,
        item.IsPriority ? "Yes" : "No",
        item.PriorityNeededBy?.ToString("yyyy-MM-dd") ?? string.Empty,
        item.Survey ?? string.Empty,
        item.Abstract ?? string.Empty,
        item.LotBlock ?? string.Empty,
        item.Subdivision ?? string.Empty,
        item.LegalDescription ?? string.Empty
    ];
}
