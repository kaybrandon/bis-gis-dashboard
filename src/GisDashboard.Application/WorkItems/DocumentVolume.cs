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

    /// <summary>
    /// Always include the Unassigned bucket when there is any uploaded volume,
    /// including a zero count when every document has an Assigned to.
    /// </summary>
    public static IReadOnlyList<NamedCount> WithUnassignedBucket(
        IEnumerable<NamedCount> assigned,
        int unassignedCount)
    {
        var rows = assigned.ToList();
        if (rows.Count == 0 && unassignedCount == 0)
        {
            return rows;
        }

        rows.Add(new NamedCount(UnassignedId, UnassignedName, null, unassignedCount));
        return rows;
    }
}
