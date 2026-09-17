namespace GisDashboard.Domain;

public sealed class InternalNoteRevision
{
    public Guid Id { get; set; }
    public Guid WorkItemId { get; set; }
    public WorkItem WorkItem { get; set; } = null!;

    public string Body { get; set; } = string.Empty;
    public Guid EditedByUserId { get; set; }
    public ApplicationUser EditedByUser { get; set; } = null!;
    public DateTimeOffset EditedAt { get; set; }
    public long EditedAtSort { get; set; }
}
