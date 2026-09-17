namespace GisDashboard.Domain;

public sealed class DocumentType
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public int SortOrder { get; set; }
}
