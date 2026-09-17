namespace GisDashboard.Domain;

/// <summary>
/// Singleton SMTP settings saved by a Global Administrator.
/// The password is stored only as a Data Protection cipher — never in API responses.
/// </summary>
public sealed class EmailSettings
{
    public static readonly Guid SingletonId = Guid.Parse("ffffffff-0000-0000-0000-000000000001");

    public Guid Id { get; set; } = SingletonId;
    public bool Enabled { get; set; }
    public string? Host { get; set; }
    public int Port { get; set; } = 587;
    public bool UseSsl { get; set; } = true;
    public string? FromAddress { get; set; }
    public string? FromName { get; set; }
    public string? UserName { get; set; }
    public string? ReplyTo { get; set; }
    public string? PasswordProtected { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }
    public Guid? UpdatedByUserId { get; set; }
}
