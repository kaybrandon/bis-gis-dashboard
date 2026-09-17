namespace GisDashboard.Domain;

public sealed class UserPresence
{
    public Guid UserId { get; set; }
    public DateTimeOffset LastSeen { get; set; }
    public long LastSeenSort { get; set; }
    public string Route { get; set; } = "/";
    public Guid? WorkItemId { get; set; }
    public bool ClockedIn { get; set; }
    public Guid? ClockWorkItemId { get; set; }
    public bool NeedsHelp { get; set; }
    public DateTimeOffset? NeedsHelpAt { get; set; }

    public ApplicationUser? User { get; set; }
}
