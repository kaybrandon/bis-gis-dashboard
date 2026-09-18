namespace GisDashboard.Application.Reports;

public static class ReportCadences
{
    public const string Monthly = "Monthly";
    public const string Annual = "Annual";

    public static bool IsAnnual(string? value) =>
        string.Equals(value, Annual, StringComparison.OrdinalIgnoreCase);
}

public sealed class GenerateReportRequest
{
    public Guid OrganizationId { get; set; }
    public string Cadence { get; set; } = ReportCadences.Monthly;
    public int Year { get; set; }
    public int? Month { get; set; }
}

public sealed class EmailReportRequest
{
    public IReadOnlyList<Guid> UserIds { get; set; } = [];
    public IReadOnlyList<string> ExtraEmails { get; set; } = [];
}

public sealed record ReportListItem(
    Guid Id,
    Guid OrganizationId,
    string OrganizationName,
    string Cadence,
    int Year,
    int Month,
    int Version,
    string MonthLabel,
    DateTimeOffset GeneratedAt,
    string GeneratedByName,
    DateTimeOffset? LastEmailedAt,
    string? LastEmailedTo,
    int EmailCount,
    bool Emailed);

public sealed record ReportDetail(
    Guid Id,
    Guid OrganizationId,
    string OrganizationName,
    string Cadence,
    int Year,
    int Month,
    int Version,
    string MonthLabel,
    DateTimeOffset GeneratedAt,
    string GeneratedByName,
    DateTimeOffset? LastEmailedAt,
    string? LastEmailedTo,
    int EmailCount,
    bool Emailed,
    bool CanEmail,
    ReportSnapshot Snapshot,
    IReadOnlyList<ReportEmailLogDto> Emails);

public sealed record ReportEmailLogDto(
    Guid Id,
    DateTimeOffset SentAt,
    string Recipients,
    string Mode,
    bool Delivered,
    string? Error);

public sealed record ReportRecipient(Guid Id, string DisplayName, string Email, string? FullName = null);

public sealed record EmailReportResult(
    bool Delivered,
    string Mode,
    string Recipients,
    string? Note);

public sealed class ReportSnapshot
{
    public string Title { get; set; } = ReportBranding.Title;
    public string Cadence { get; set; } = ReportCadences.Monthly;
    public bool IncludeParcelStatus { get; set; } = true;
    public string OrganizationName { get; set; } = string.Empty;
    public string MonthName { get; set; } = string.Empty;
    public string MonthLabel { get; set; } = string.Empty;
    public string PeriodPhrase { get; set; } = "this month";
    public string Intro { get; set; } = string.Empty;
    public string Closing { get; set; } = ReportBranding.Closing;
    public ReportContact Contact { get; set; } = new();
    public ReportParcelStatus ParcelStatus { get; set; } = new();
    public int Completed { get; set; }
    public IReadOnlyList<ReportNamedCount> MaintenanceByType { get; set; } = [];
    public IReadOnlyList<ReportCompletedItem> CompletedItems { get; set; } = [];
    public int Uploaded { get; set; }
    public int Pending { get; set; }
    public int Active { get; set; }
    public int OnHold { get; set; }
    public decimal Hours { get; set; }
    public string HoursLabel { get; set; } = "0m";
}

public sealed class ReportContact
{
    public string Phone { get; set; } = ReportBranding.Phone;
    public string Email { get; set; } = ReportBranding.Email;
    public string Department { get; set; } = ReportBranding.Department;
    public string Company { get; set; } = ReportBranding.Company;
    public string Website { get; set; } = ReportBranding.Website;
}

public sealed class ReportParcelStatus
{
    public bool Available { get; set; }
    public string Note { get; set; } = ReportBranding.ParcelUnavailableNote;
    public int? TotalRealAccounts { get; set; }
    public int? ParcelsWithOwnership { get; set; }
    public int? MissingRealAccounts { get; set; }
    public decimal? PercentComplete { get; set; }
}

public sealed record ReportNamedCount(string Name, int Count, string? Color);

public sealed record ReportCompletedItem(
    string FileName,
    DateTimeOffset UploadedAt,
    DateTimeOffset? WorkedOn,
    int Annexations,
    int Corrections,
    int Plats,
    int Deeds,
    bool Sketch,
    string PropertyIds);

public static class ReportBranding
{
    public const string Title = "GIS Maintenance Report";
    public const string AnnualTitle = "GIS Annual Maintenance Report";
    public const string Phone = "(800) 247-9045";
    public const string Email = "gissupport@bisconsultants.com";
    public const string Department = "GIS Department";
    public const string Company = "BIS Consultants";
    public const string Website = "www.bisconsultants.com";
    public const string ParcelUnavailableNote =
        "Parcel inventory is not stored in GIS Dashboard yet. Enter Total Real Accounts and Parcels With Ownership on the organization to include them here.";
    public const string Closing =
        "If you have any questions about this report, please email or call us at the office!";

    public static string Intro(string monthName) =>
        $"This is your monthly GIS Maintenance Report for the month of {monthName}. Below you will find your current mapped status, the number of work items that were completed, as well as a detailed listing of those completed items.";

    public static string AnnualIntro(int year) =>
        $"This is your annual GIS Maintenance Report for {year}. Below you will find the number of work items that were completed, as well as a detailed listing of those completed items.";
}

public sealed record ReportFile(byte[] Content, string FileName, string ContentType);
