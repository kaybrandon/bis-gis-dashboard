namespace GisDashboard.Domain;

public sealed class MonthlyReport
{
    public Guid Id { get; set; }
    public Guid OrganizationId { get; set; }
    public Organization Organization { get; set; } = null!;

    public int Year { get; set; }
    /// <summary>1–12 for monthly reports; 0 for annual.</summary>
    public int Month { get; set; }
    public string Cadence { get; set; } = "Monthly";
    public int Version { get; set; }
    public string MonthLabel { get; set; } = string.Empty;

    public DateTimeOffset PeriodStart { get; set; }
    public DateTimeOffset PeriodEnd { get; set; }
    public DateTimeOffset GeneratedAt { get; set; }
    public Guid GeneratedByUserId { get; set; }
    public ApplicationUser GeneratedByUser { get; set; } = null!;

    public string SnapshotJson { get; set; } = "{}";

    public DateTimeOffset? LastEmailedAt { get; set; }
    public string? LastEmailedTo { get; set; }
    public int EmailCount { get; set; }

    public ICollection<ReportEmailLog> Emails { get; set; } = new List<ReportEmailLog>();
}

public sealed class ReportEmailLog
{
    public Guid Id { get; set; }
    public Guid ReportId { get; set; }
    public MonthlyReport Report { get; set; } = null!;

    public DateTimeOffset SentAt { get; set; }
    public string Recipients { get; set; } = string.Empty;
    public string Subject { get; set; } = string.Empty;
    public string Mode { get; set; } = "dry-run";
    public bool Delivered { get; set; }
    public string? Error { get; set; }
    public Guid SentByUserId { get; set; }
}
