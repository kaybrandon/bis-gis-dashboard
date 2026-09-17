namespace GisDashboard.Domain;

public sealed class WorkItem
{
    public Guid Id { get; set; }
    public Guid OrganizationId { get; set; }
    public Organization Organization { get; set; } = null!;

    public string FileName { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;

    public Guid DocumentTypeId { get; set; }
    public DocumentType DocumentType { get; set; } = null!;

    public Guid StatusId { get; set; }
    public WorkItemStatus Status { get; set; } = null!;

    public Guid? AssignedToUserId { get; set; }
    public ApplicationUser? AssignedToUser { get; set; }

    public string? InternalNotes { get; set; }
    public ICollection<InternalNoteRevision> NoteRevisions { get; set; } = new List<InternalNoteRevision>();
    public ICollection<TimeEntry> TimeEntries { get; set; } = new List<TimeEntry>();
    public ICollection<WorkItemComment> Comments { get; set; } = new List<WorkItemComment>();

    public bool IsSplit { get; set; }
    public bool IsSketch { get; set; }
    public bool IsPriority { get; set; }
    public bool IsReviewed { get; set; }
    public string? PriorityNote { get; set; }
    public DateTimeOffset? PriorityNeededBy { get; set; }
    public long? PriorityNeededBySort { get; set; }
    public DateTimeOffset? PriorityRequestedAt { get; set; }
    public Guid? PriorityRequestedByUserId { get; set; }
    public DateTimeOffset? PriorityAcknowledgedAt { get; set; }
    public Guid? PriorityAcknowledgedByUserId { get; set; }

    public DateTimeOffset? WorkedOn { get; set; }
    public long? WorkedOnSort { get; set; }
    public DateTimeOffset? FirstDeadline { get; set; }
    public long? FirstDeadlineSort { get; set; }
    public DateTimeOffset? FinalDeadline { get; set; }
    public long? FinalDeadlineSort { get; set; }

    public int AnnexationCount { get; set; }
    public int CorrectionCount { get; set; }
    public int DeedCount { get; set; }
    public int PlatCount { get; set; }
    public string? PropertyIds { get; set; }

    public string? BlobPath { get; set; }
    public string? ContentType { get; set; }
    public long FileSizeBytes { get; set; }

    public DateTimeOffset UploadedAt { get; set; }
    public long UploadedAtSort { get; set; }
    public Guid UploadedByUserId { get; set; }
    public ApplicationUser UploadedByUser { get; set; } = null!;

    public DateTimeOffset UpdatedAt { get; set; }
    public long UpdatedAtSort { get; set; }
    public Guid UpdatedByUserId { get; set; }

    public void TouchDates(DateTimeOffset uploadedAt, DateTimeOffset updatedAt)
    {
        UploadedAt = uploadedAt;
        UploadedAtSort = uploadedAt.ToUnixTimeMilliseconds();
        UpdatedAt = updatedAt;
        UpdatedAtSort = updatedAt.ToUnixTimeMilliseconds();
    }

    public void SetWorkedOn(DateTimeOffset? value)
    {
        WorkedOn = value;
        WorkedOnSort = value?.ToUnixTimeMilliseconds();
    }

    public void SetFirstDeadline(DateTimeOffset? value)
    {
        FirstDeadline = value;
        FirstDeadlineSort = value?.ToUnixTimeMilliseconds();
    }

    public void SetFinalDeadline(DateTimeOffset? value)
    {
        FinalDeadline = value;
        FinalDeadlineSort = value?.ToUnixTimeMilliseconds();
    }

    public bool ApplyPriority(bool isPriority, string? note, Guid? actorUserId, DateTimeOffset now)
    {
        var becamePriority = isPriority && !IsPriority;
        IsPriority = isPriority;
        if (!string.IsNullOrWhiteSpace(note))
        {
            var trimmed = note.Trim();
            PriorityNote = trimmed.Length > 500 ? trimmed[..500] : trimmed;
        }
        else if (!isPriority)
        {
            PriorityNote = null;
            SetPriorityNeededBy(null);
        }

        if (isPriority)
        {
            PriorityRequestedAt = now;
            PriorityRequestedByUserId = actorUserId;
            PriorityAcknowledgedAt = null;
            PriorityAcknowledgedByUserId = null;
        }
        else
        {
            PriorityAcknowledgedAt = now;
            PriorityAcknowledgedByUserId = actorUserId;
        }

        return becamePriority;
    }

    public void SetPriorityNeededBy(DateTimeOffset? neededBy)
    {
        if (!IsPriority || neededBy is null)
        {
            PriorityNeededBy = null;
            PriorityNeededBySort = null;
            return;
        }

        var utc = neededBy.Value.ToUniversalTime();
        var day = new DateTimeOffset(utc.Year, utc.Month, utc.Day, 0, 0, 0, TimeSpan.Zero);
        PriorityNeededBy = day;
        PriorityNeededBySort = day.ToUnixTimeMilliseconds();
    }
}
