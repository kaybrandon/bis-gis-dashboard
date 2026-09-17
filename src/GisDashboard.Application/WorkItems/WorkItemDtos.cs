namespace GisDashboard.Application.WorkItems;

public sealed record WorkItemListItem(
    Guid Id,
    string FileName,
    string Title,
    Guid OrganizationId,
    string OrganizationName,
    Guid DocumentTypeId,
    string DocumentTypeName,
    Guid StatusId,
    string StatusName,
    string StatusColor,
    string? AssignedToName,
    Guid? AssignedToUserId,
    DateTimeOffset? PriorityNeededBy,
    DateTimeOffset UploadedAt,
    string UploadedByName,
    DateTimeOffset UpdatedAt,
    DateTimeOffset? WorkedOn,
    decimal Hours,
    string HoursLabel,
    DateTimeOffset? FirstDeadline,
    DateTimeOffset? FinalDeadline,
    string? ContentType,
    long FileSizeBytes,
    bool IsPriority,
    string? PriorityNote,
    bool IsReviewed);

public sealed record NamedCount(Guid Id, string Name, string? Color, int Count);

public sealed record WorkItemListResponse(
    IReadOnlyList<WorkItemListItem> Items,
    int Total,
    int Page,
    int PageSize,
    IReadOnlyList<NamedCount> StatusCounts,
    IReadOnlyList<NamedCount> TypeCounts,
    BucketCounts Buckets);

public sealed record WorkItemDetail(
    Guid Id,
    string FileName,
    string Title,
    Guid OrganizationId,
    string OrganizationName,
    Guid DocumentTypeId,
    string DocumentTypeName,
    Guid StatusId,
    string StatusName,
    string StatusColor,
    Guid? AssignedToUserId,
    string? AssignedToName,
    string? InternalNotes,
    bool CanSeeInternalNotes,
    bool CanEditInternalNotes,
    DateTimeOffset? InternalNotesUpdatedAt,
    string? InternalNotesUpdatedByName,
    IReadOnlyList<NoteRevisionDto> InternalNotesHistory,
    bool CanSeeTimeLogs,
    bool CanLogTime,
    bool CanMutate,
    bool CanPostComments,
    bool IsSplit,
    bool IsSketch,
    DateTimeOffset? WorkedOn,
    decimal Hours,
    string HoursLabel,
    DateTimeOffset? FirstDeadline,
    DateTimeOffset? FinalDeadline,
    int AnnexationCount,
    int CorrectionCount,
    int DeedCount,
    int PlatCount,
    string? PropertyIds,
    DateTimeOffset UploadedAt,
    string UploadedByName,
    DateTimeOffset UpdatedAt,
    string? ContentType,
    long FileSizeBytes,
    bool IsPriority,
    string? PriorityNote,
    DateTimeOffset? PriorityRequestedAt,
    string? PriorityRequestedByName,
    bool CanSetPriority,
    bool IsReviewed,
    DateTimeOffset? PriorityNeededBy);

public sealed record WorkItemNeighbors(Guid? PreviousId, Guid? NextId);

public sealed class WorkItemQuery
{
    public string? Search { get; set; }
    public Guid? OrganizationId { get; set; }
    public Guid? DocumentTypeId { get; set; }
    public Guid? StatusId { get; set; }
    public Guid? AssignedToUserId { get; set; }
    public DateTimeOffset? UploadedFrom { get; set; }
    public DateTimeOffset? UploadedTo { get; set; }
    public DateTimeOffset? WorkedFrom { get; set; }
    public DateTimeOffset? WorkedTo { get; set; }
    public string? Bucket { get; set; }
    public string? GroupBy { get; set; }
    public string SortBy { get; set; } = "uploadedAt";
    public string SortDir { get; set; } = "desc";
    public int Page { get; set; } = 1;
    public int PageSize { get; set; } = 25;
}

public sealed class UpdateWorkItemRequest
{
    public string? Title { get; set; }
    public Guid? StatusId { get; set; }
    public Guid? DocumentTypeId { get; set; }
    public Guid? AssignedToUserId { get; set; }
    public bool ClearAssignment { get; set; }
    public string? InternalNotes { get; set; }
    public bool? IsSplit { get; set; }
    public bool? IsSketch { get; set; }
    public DateTimeOffset? WorkedOn { get; set; }
    public bool ClearWorkedOn { get; set; }
    public DateTimeOffset? FirstDeadline { get; set; }
    public bool ClearFirstDeadline { get; set; }
    public DateTimeOffset? FinalDeadline { get; set; }
    public bool ClearFinalDeadline { get; set; }
    public int? AnnexationCount { get; set; }
    public int? CorrectionCount { get; set; }
    public int? DeedCount { get; set; }
    public int? PlatCount { get; set; }
    public string? PropertyIds { get; set; }
    public bool? IsPriority { get; set; }
    public string? PriorityNote { get; set; }
    public DateTimeOffset? PriorityNeededBy { get; set; }
    public bool ClearPriorityNeededBy { get; set; }
    public bool? IsReviewed { get; set; }
}

public sealed class UploadWorkItemRequest
{
    public Guid OrganizationId { get; set; }
    public string? OrganizationName { get; set; }
    public Guid DocumentTypeId { get; set; }
    public string? DocumentTypeName { get; set; }
    public string? Title { get; set; }
    public Guid? AssignedToUserId { get; set; }
    public required string FileName { get; set; }
    public required string ContentType { get; set; }
    public required Stream Content { get; set; }
    public long FileSizeBytes { get; set; }
    public bool IsPriority { get; set; }
    public string? PriorityNote { get; set; }
    public string? ClientNotes { get; set; }
}

public sealed record FileDownload(Stream Content, string ContentType, string FileName);

public sealed record ExcelExport(byte[] Content, string FileName, string ContentType);
