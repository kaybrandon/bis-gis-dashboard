namespace GisDashboard.Domain;

public sealed class UserOrganization
{
    public Guid UserId { get; set; }
    public Guid OrganizationId { get; set; }

    public ApplicationUser User { get; set; } = null!;
    public Organization Organization { get; set; } = null!;
}
