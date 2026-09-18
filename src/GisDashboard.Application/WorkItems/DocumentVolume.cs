namespace GisDashboard.Application.WorkItems;

/// <summary>
/// CR10 — document counts by CAD and technician. v1 CAD is Organization (client) name.
/// Technician is work-item Assigned to. Date is created/uploaded in the selected range.
/// </summary>
public static class DocumentVolume
{
    /// <summary>Sentinel for documents with no Assigned to. Distinct from org Assigned technician.</summary>
    public static readonly Guid UnassignedId = Guid.Empty;

    public const string UnassignedName = "Unassigned";
    public const string CadTitle = "Documents by CAD";
    public const string TechnicianTitle = "Documents by technician";
    public const string CadHelp =
        "v1 CAD is the Organization (client) name. Counts are documents created or uploaded in the selected date range.";
    public const string TechnicianHelp =
        "Technician is the document Assigned to, not the organization’s Assigned technician. Unassigned is work with no assignee.";
}
