namespace GisDashboard.Domain;

public sealed class WorkItemComment
{
    public Guid Id { get; set; }
    public Guid WorkItemId { get; set; }
    public WorkItem WorkItem { get; set; } = null!;

    public Guid AuthorUserId { get; set; }
    public ApplicationUser AuthorUser { get; set; } = null!;

    public string Body { get; set; } = string.Empty;
    public DateTimeOffset CreatedAt { get; set; }
    public long CreatedAtSort { get; set; }
}
