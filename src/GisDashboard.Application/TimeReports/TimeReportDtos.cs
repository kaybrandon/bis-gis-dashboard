namespace GisDashboard.Application.TimeReports;

public sealed class TimeReportQuery
{
    public DateOnly? From { get; set; }
    public DateOnly? To { get; set; }
    public Guid? OrganizationId { get; set; }
    public Guid? UserId { get; set; }
    public string? Bucket { get; set; }
}

public sealed record TimeReportNamedTotal(
    Guid Id,
    string Name,
    int Minutes,
    decimal Hours,
    string HoursLabel,
    int EntryCount);

public sealed record TimeReportPeriodTotal(
    string Key,
    string Label,
    int Minutes,
    decimal Hours,
    string HoursLabel,
    int EntryCount);

public sealed record TimeReportWorkItemTotal(
    Guid Id,
    string FileName,
    string OrganizationName,
    int Minutes,
    decimal Hours,
    string HoursLabel,
    int EntryCount);

public sealed record TimeReportLine(
    DateTimeOffset WorkedOn,
    string LoggedByName,
    string OrganizationName,
    string FileName,
    Guid WorkItemId,
    int Minutes,
    decimal Hours,
    string HoursLabel,
    string? Note);

public sealed record TimeReportPersonOption(Guid Id, string Name);

public sealed record TimeReportResponse(
    DateOnly From,
    DateOnly To,
    string Bucket,
    bool CanViewTeam,
    int TotalMinutes,
    decimal TotalHours,
    string TotalLabel,
    int PeopleCount,
    int ClientCount,
    int WorkItemCount,
    IReadOnlyList<TimeReportPersonOption> People,
    IReadOnlyList<TimeReportNamedTotal> ByPerson,
    IReadOnlyList<TimeReportNamedTotal> ByClient,
    IReadOnlyList<TimeReportPeriodTotal> ByPeriod,
    IReadOnlyList<TimeReportWorkItemTotal> ByWorkItem,
    IReadOnlyList<TimeReportLine> Entries);

public sealed record TimeReportFile(byte[] Content, string FileName, string ContentType);
