namespace GisDashboard.Infrastructure.Email;

public sealed class EmailOptions
{
    public const string SectionName = "Email";

    public bool Enabled { get; set; }
    public string From { get; set; } = "noreply@bisconsultants.local";
    public string FromName { get; set; } = "GIS Dashboard";
    public string? ReplyTo { get; set; }
    public string? PublicBaseUrl { get; set; }
    public SmtpOptions Smtp { get; set; } = new();
}

public sealed class SmtpOptions
{
    public string? Host { get; set; }
    public int Port { get; set; } = 587;
    public string? User { get; set; }
    public string? Password { get; set; }
    public bool UseSsl { get; set; } = true;
}
