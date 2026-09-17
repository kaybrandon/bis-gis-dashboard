namespace GisDashboard.Domain;

public sealed class FileServer
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string RootPath { get; set; } = string.Empty;
    public DateTimeOffset CreatedAt { get; set; }
}
