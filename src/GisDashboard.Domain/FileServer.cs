namespace GisDashboard.Domain;

public sealed class FileServer
{
    public Guid Id { get; set; }
    public string? Name { get; set; }
    public string? RootPath { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
}
