namespace GisDashboard.Domain;

public sealed class TimeEntry
{
    public Guid Id { get; set; }
    public Guid WorkItemId { get; set; }
    public WorkItem WorkItem { get; set; } = null!;

    public Guid LoggedByUserId { get; set; }
    public ApplicationUser LoggedByUser { get; set; } = null!;

    public int Minutes { get; set; }
    public DateTimeOffset WorkedOn { get; set; }
    public long WorkedOnSort { get; set; }
    public string? Note { get; set; }

    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }
    public long CreatedAtSort { get; set; }

    public void TouchWorkedOn(DateTimeOffset workedOn)
    {
        WorkedOn = workedOn;
        WorkedOnSort = workedOn.ToUnixTimeMilliseconds();
    }

    public void TouchCreated(DateTimeOffset createdAt)
    {
        CreatedAt = createdAt;
        CreatedAtSort = createdAt.ToUnixTimeMilliseconds();
        UpdatedAt = createdAt;
    }
}
