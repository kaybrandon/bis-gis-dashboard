namespace GisDashboard.Domain;

public sealed class SyncControlState
{
    public static readonly Guid SingletonId = Guid.Parse("FFFFFFFF-0000-0000-0000-000000000010");

    public Guid Id { get; set; } = SingletonId;
    public bool Paused { get; set; }
    public string? Message { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }
}
