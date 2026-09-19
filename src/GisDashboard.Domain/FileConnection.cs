namespace GisDashboard.Domain;

/// <summary>Source / optional FTP publish row returned by GET /api/connections.</summary>
public sealed class FileConnection
{
    public Guid Id { get; set; }
    public Guid OrganizationId { get; set; }
    public Organization? Organization { get; set; }
    public Guid? FileServerId { get; set; }
    public FileServer? FileServer { get; set; }
    public string? SourcePath { get; set; }
    public string? FtpFolder { get; set; }
    public string? FtpUrl { get; set; }
    public string? FtpUserName { get; set; }
    public string? FtpPasswordProtected { get; set; }
    public bool Enabled { get; set; } = true;
    public string? Status { get; set; }
    public string? LastError { get; set; }
    public DateTimeOffset? LastPublishedAt { get; set; }
    public int LastFileCount { get; set; }
    public string? LastZipName { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }
}
