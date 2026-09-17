namespace GisDashboard.Domain;

public sealed class InAppNotification
{
    public Guid Id { get; set; }
    public Guid UserId { get; set; }
    public ApplicationUser User { get; set; } = null!;
    public Guid OrganizationId { get; set; }
    public Guid? WorkItemId { get; set; }
    public string Kind { get; set; } = "priority";
    public string Title { get; set; } = string.Empty;
    public string Body { get; set; } = string.Empty;
    public DateTimeOffset CreatedAt { get; set; }
    public long CreatedAtSort { get; set; }
    public DateTimeOffset? ReadAt { get; set; }
}
