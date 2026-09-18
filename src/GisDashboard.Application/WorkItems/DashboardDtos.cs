using GisDashboard.Application.Directory;

namespace GisDashboard.Application.WorkItems;

public sealed class DashboardQuery
{
    public Guid? OrganizationId { get; set; }
    public Guid? StatusId { get; set; }
    public Guid? AssignedToUserId { get; set; }
    /// <summary>CR08 — items with no work-item Assigned to. Distinct from org Assigned technician.</summary>
    public bool UnassignedOnly { get; set; }
    public DateTimeOffset? From { get; set; }
    public DateTimeOffset? To { get; set; }
}

public sealed record DashboardKpi(string Key, string Label, int Count, string? Color);

public sealed record DayVolume(string Date, int Uploaded, int Completed);

public sealed record HoursSlice(Guid Id, string Name, decimal Hours);

public sealed record DashboardResponse(
    IReadOnlyList<DashboardKpi> Kpis,
    IReadOnlyList<NamedCount> StatusCounts,
    /// <summary>CR10 — document counts by technician (Assigned to), including Unassigned.</summary>
    IReadOnlyList<NamedCount> AssigneeCounts,
    /// <summary>CR10 — document counts by CAD. v1 CAD is Organization (client) name.</summary>
    IReadOnlyList<NamedCount> OrganizationCounts,
    IReadOnlyList<WorkItemListItem> RecentCompleted,
    IReadOnlyList<DayVolume> VolumeOverTime,
    IReadOnlyList<HoursSlice> HoursByAssignee,
    IReadOnlyList<HoursSlice> HoursByClient,
    DateTimeOffset From,
    DateTimeOffset To,
    string RangeLabel);

public sealed class DashboardEmailRequest
{
    public List<Guid>? UserIds { get; set; }
    public List<string>? ExtraEmails { get; set; }
}

public sealed record DashboardEmailResult(
    bool Delivered,
    string Mode,
    string Recipients,
    string? Note);

public sealed record DashboardRecipient(Guid Id, string DisplayName, string Email, string? FullName = null);

public sealed record AssignedTechnicianDisplay(string Name, bool IsPrimary);

public sealed record PublicUploadInfo(
    string OrganizationName,
    IReadOnlyList<LookupItem> DocumentTypes,
    long MaxFileBytes,
    int MaxFileMegabytes,
    int Concurrency,
    IReadOnlyList<AssignedTechnicianDisplay> AssignedTechnicians,
    IReadOnlyList<string> AcceptedExtensions,
    string SupportedTypesLabel,
    string Accept);

public sealed record PublicUploadResult(
    Guid Id,
    string FileName,
    string OrganizationName,
    string StatusName);

public sealed record BucketCounts(
    int Pending,
    int Mine,
    int OnHold,
    int Completed,
    int FirstDeadline,
    int FinalDeadline,
    int Priority,
    int DueThisWeek,
    int Unassigned);

public sealed class PublicUploadReceivedRequest
{
    public int FileCount { get; set; }
    public IReadOnlyList<string>? FileNames { get; set; }
}

public sealed record StatusActions(
    Guid PendingId,
    Guid ActiveId,
    Guid OnHoldId,
    Guid CompleteId,
    Guid CancelledId,
    Guid NeedsReviewId,
    IReadOnlyList<Guid> CompletedIds);

public sealed record CommentDto(
    Guid Id,
    Guid WorkItemId,
    string Body,
    Guid AuthorUserId,
    string AuthorName,
    DateTimeOffset CreatedAt);

public sealed class CreateCommentRequest
{
    public string Body { get; set; } = string.Empty;
}
