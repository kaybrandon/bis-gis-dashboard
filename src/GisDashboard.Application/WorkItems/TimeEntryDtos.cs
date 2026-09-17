namespace GisDashboard.Application.WorkItems;

public sealed record TimeEntryDto(
    Guid Id,
    Guid WorkItemId,
    int Minutes,
    decimal Hours,
    string DurationLabel,
    DateTimeOffset WorkedOn,
    string? Note,
    Guid LoggedByUserId,
    string LoggedByName,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt,
    bool CanEdit);

public sealed record TimeEntryListResponse(
    IReadOnlyList<TimeEntryDto> Items,
    int TotalMinutes,
    string TotalLabel);

public sealed class UpsertTimeEntryRequest
{
    public int? Hours { get; set; }
    public int? Minutes { get; set; }
    public decimal? DecimalHours { get; set; }
    public DateTimeOffset? WorkedOn { get; set; }
    public string? Note { get; set; }
}

public sealed record NoteRevisionDto(
    Guid Id,
    string Body,
    DateTimeOffset EditedAt,
    string EditedByName);
