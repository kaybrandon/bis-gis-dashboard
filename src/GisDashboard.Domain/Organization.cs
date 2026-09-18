namespace GisDashboard.Domain;

public sealed class Organization
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Code { get; set; } = string.Empty;
    public bool IsActive { get; set; } = true;
    public DateTimeOffset CreatedAt { get; set; }
    public string? UploadToken { get; set; }
    public DateTimeOffset? UploadTokenCreatedAt { get; set; }

    /// <summary>Client-supplied parcel inventory for the maintenance report. Not sourced from a CAD extract.</summary>
    public int? ParcelTotalRealAccounts { get; set; }
    public int? ParcelWithOwnership { get; set; }

    /// <summary>When true, client Viewers and Uploaders (and org Admins as a team view) can open time report cards for this organization.</summary>
    public bool TimeReportCardsVisible { get; set; }

    public ICollection<UserOrganization> Members { get; set; } = new List<UserOrganization>();
    public ICollection<OrganizationTech> AssignedTechs { get; set; } = new List<OrganizationTech>();
    public ICollection<WorkItem> WorkItems { get; set; } = new List<WorkItem>();
}
