namespace GisDashboard.Domain;

public sealed class WorkItemStatus
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Color { get; set; } = "#1890ff";
    public int SortOrder { get; set; }
}
