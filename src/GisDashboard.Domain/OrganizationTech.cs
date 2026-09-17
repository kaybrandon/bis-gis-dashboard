namespace GisDashboard.Domain;

public sealed class OrganizationTech
{
    public Guid OrganizationId { get; set; }
    public Organization Organization { get; set; } = null!;
    public Guid UserId { get; set; }
    public ApplicationUser User { get; set; } = null!;
    public bool IsPrimary { get; set; }
}
