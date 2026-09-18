namespace GisDashboard.Domain;

public sealed class HelpMessage
{
    public Guid Id { get; set; }
    public Guid FromUserId { get; set; }
    public Guid ToUserId { get; set; }
    public string? Chip { get; set; }
    public string Body { get; set; } = string.Empty;
    public DateTimeOffset CreatedAt { get; set; }
    public long CreatedAtSort { get; set; }
    public DateTimeOffset? ReadAt { get; set; }

    public ApplicationUser? FromUser { get; set; }
    public ApplicationUser? ToUser { get; set; }
}
