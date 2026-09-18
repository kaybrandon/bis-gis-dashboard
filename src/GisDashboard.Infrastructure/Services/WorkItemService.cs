using GisDashboard.Application.Abstractions;
using GisDashboard.Application.Directory;
using GisDashboard.Application.Exceptions;
using GisDashboard.Application.Notifications;
using GisDashboard.Application.WorkItems;
using GisDashboard.Domain;
using GisDashboard.Infrastructure.Export;
using GisDashboard.Infrastructure.Persistence;
using GisDashboard.Infrastructure.Preview;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace GisDashboard.Infrastructure.Services;

public sealed class WorkItemService : IWorkItemService
{
    private readonly AppDbContext _db;
    private readonly ICurrentUser _currentUser;
    private readonly IOrgScope _orgScope;
    private readonly IFileStorage _storage;
    private readonly INotificationService _notifications;
    private readonly IWorkflowComms _comms;
    private readonly UploadOptions _uploads;

    public WorkItemService(
        AppDbContext db,
        ICurrentUser currentUser,
        IOrgScope orgScope,
        IFileStorage storage,
        INotificationService notifications,
        IWorkflowComms comms,
        IOptions<UploadOptions> uploads)
    {
        _db = db;
        _currentUser = currentUser;
        _orgScope = orgScope;
        _storage = storage;
        _notifications = notifications;
        _comms = comms;
        _uploads = uploads.Value;
    }

    public async Task<WorkItemListResponse> ListAsync(WorkItemQuery query, CancellationToken cancellationToken = default)
    {
        await EnsureOrganizationFilterAsync(query.OrganizationId, cancellationToken);
        var allowed = await _orgScope.GetAllowedOrganizationIdsAsync(cancellationToken);
        var scoped = ApplyFilters(BaseQuery(allowed), query);
        var assigneeAgnostic = ApplyFilters(BaseQuery(allowed), query, ignoreAssignee: true);
        var buckets = await ComputeBucketsAsync(scoped, assigneeAgnostic, cancellationToken);
        var filtered = ApplyBucket(scoped, query.Bucket);

        var statusCounts = await filtered
            .GroupBy(x => new { x.StatusId, x.Status.Name, x.Status.Color, x.Status.SortOrder })
            .Select(g => new { g.Key.StatusId, g.Key.Name, g.Key.Color, g.Key.SortOrder, Count = g.Count() })
            .OrderBy(x => x.SortOrder)
            .ToListAsync(cancellationToken);

        var typeCounts = await filtered
            .GroupBy(x => new { x.DocumentTypeId, x.DocumentType.Name, x.DocumentType.SortOrder })
            .Select(g => new { g.Key.DocumentTypeId, g.Key.Name, g.Key.SortOrder, Count = g.Count() })
            .OrderBy(x => x.SortOrder)
            .ToListAsync(cancellationToken);

        var total = statusCounts.Sum(x => x.Count);
        var page = Math.Max(query.Page, 1);
        var pageSize = Math.Clamp(query.PageSize, 1, 100);

        var rows = await ApplySort(filtered, query)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(x => new
            {
                x.Id,
                x.FileName,
                x.Title,
                x.OrganizationId,
                OrganizationName = x.Organization.Name,
                x.DocumentTypeId,
                DocumentTypeName = x.DocumentType.Name,
                x.StatusId,
                StatusName = x.Status.Name,
                StatusColor = x.Status.Color,
                AssignedToName = x.AssignedToUser != null
                    ? (x.AssignedToUser.FullName != null && x.AssignedToUser.FullName != ""
                        ? x.AssignedToUser.FullName
                        : x.AssignedToUser.DisplayName)
                    : null,
                AssignedToIsArchived = x.AssignedToUser != null && x.AssignedToUser.IsArchived,
                x.AssignedToUserId,
                x.PriorityNeededBy,
                x.UploadedAt,
                UploadedByName = x.UploadedByUser.FullName != null && x.UploadedByUser.FullName != ""
                    ? x.UploadedByUser.FullName
                    : x.UploadedByUser.DisplayName,
                UploadedByIsArchived = x.UploadedByUser.IsArchived,
                x.UpdatedAt,
                x.WorkedOn,
                Minutes = x.TimeEntries.Sum(t => t.Minutes),
                x.FirstDeadline,
                x.FinalDeadline,
                x.ContentType,
                x.FileSizeBytes,
                x.IsPriority,
                x.PriorityNote,
                x.IsReviewed
            })
            .ToListAsync(cancellationToken);

        var items = rows.Select(x => new WorkItemListItem(
            x.Id,
            x.FileName,
            x.Title,
            x.OrganizationId,
            x.OrganizationName,
            x.DocumentTypeId,
            x.DocumentTypeName,
            x.StatusId,
            StatusDisplay.Label(x.StatusName),
            x.StatusColor,
            UserIdentity.WithArchivedSuffix(x.AssignedToName, x.AssignedToIsArchived),
            x.AssignedToUserId,
            x.PriorityNeededBy,
            x.UploadedAt,
            UserIdentity.WithArchivedSuffix(x.UploadedByName, x.UploadedByIsArchived),
            x.UpdatedAt,
            x.WorkedOn,
            TimeDurations.ToHours(x.Minutes),
            TimeDurations.Format(x.Minutes),
            x.FirstDeadline,
            x.FinalDeadline,
            x.ContentType,
            x.FileSizeBytes,
            x.IsPriority,
            x.PriorityNote,
            x.IsReviewed)).ToList();

        return new WorkItemListResponse(
            items,
            total,
            page,
            pageSize,
            statusCounts.Select(x => new NamedCount(x.StatusId, StatusDisplay.Label(x.Name), x.Color, x.Count)).ToList(),
            typeCounts.Select(x => new NamedCount(x.DocumentTypeId, x.Name, null, x.Count)).ToList(),
            buckets);
    }

    public async Task<WorkItemDetail> GetAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var item = await LoadScopedAsync(id, cancellationToken);
        return MapDetail(item);
    }

    public async Task<WorkItemNeighbors> GetNeighborsAsync(Guid id, WorkItemQuery query, CancellationToken cancellationToken = default)
    {
        await LoadScopedAsync(id, cancellationToken);
        await EnsureOrganizationFilterAsync(query.OrganizationId, cancellationToken);
        var allowed = await _orgScope.GetAllowedOrganizationIdsAsync(cancellationToken);
        var ids = await ApplySort(ApplyBucket(ApplyFilters(BaseQuery(allowed), query), query.Bucket), query)
            .Select(x => x.Id)
            .ToListAsync(cancellationToken);

        var index = ids.IndexOf(id);
        if (index < 0)
        {
            return new WorkItemNeighbors(null, null);
        }

        return new WorkItemNeighbors(
            index > 0 ? ids[index - 1] : null,
            index < ids.Count - 1 ? ids[index + 1] : null);
    }

    public async Task<WorkItemDetail> UploadAsync(UploadWorkItemRequest request, CancellationToken cancellationToken = default)
    {
        if (!_currentUser.CanUpload)
        {
            throw new ForbiddenException("Your role cannot upload documents.");
        }

        var organizationId = await _orgScope.ResolveAccessibleOrganizationAsync(
            request.OrganizationId,
            request.OrganizationName,
            cancellationToken);
        var documentTypeId = await ResolveDocumentTypeAsync(
            request.DocumentTypeId,
            request.DocumentTypeName,
            cancellationToken,
            allowDefault: !_currentUser.CanMutateWorkItems);

        if (string.IsNullOrWhiteSpace(request.FileName))
        {
            throw new ValidationException("A file is required.");
        }

        EnsureWithinSizeLimit(request);

        if (!UploadFileTypes.TryResolve(request.FileName, request.ContentType, out var contentType))
        {
            throw new ValidationException(UploadFileTypes.UnsupportedFileMessage(request.FileName));
        }

        Guid? assignee = null;
        if (_currentUser.CanMutateWorkItems && request.AssignedToUserId is { } requested)
        {
            assignee = requested;
        }

        assignee ??= await ResolveDefaultAssigneeAsync(organizationId, cancellationToken);
        if (assignee is { } nextAssignee)
        {
            await EnsureAssignableAsync(nextAssignee, organizationId, cancellationToken);
        }

        var now = DateTimeOffset.UtcNow;
        var item = new WorkItem
        {
            Id = Guid.NewGuid(),
            OrganizationId = organizationId,
            FileName = Path.GetFileName(request.FileName),
            Title = string.IsNullOrWhiteSpace(request.Title) ? Path.GetFileName(request.FileName) : request.Title.Trim(),
            DocumentTypeId = documentTypeId,
            StatusId = SeedIds.StatusPending,
            AssignedToUserId = assignee,
            UploadedByUserId = _currentUser.UserId,
            UpdatedByUserId = _currentUser.UserId,
            ContentType = contentType
        };
        item.TouchDates(now, now);

        var blobPath = await _storage.SaveAsync(
            item.OrganizationId,
            item.Id,
            item.FileName,
            request.Content,
            contentType,
            cancellationToken);

        item.BlobPath = blobPath;
        item.FileSizeBytes = request.Content.CanSeek ? request.Content.Length : 0;
        var becamePriority = false;
        if (request.IsPriority && _currentUser.CanMutateWorkItems)
        {
            becamePriority = item.ApplyPriority(true, request.PriorityNote, _currentUser.UserId, now);
        }

        _db.WorkItems.Add(item);
        AddClientNotesComment(item.Id, request.ClientNotes, _currentUser.UserId);
        await _db.SaveChangesAsync(cancellationToken);
        if (becamePriority)
        {
            await _notifications.NotifyPriorityAsync(item, _currentUser.UserId, cancellationToken);
        }

        return MapDetail(await LoadScopedAsync(item.Id, cancellationToken));
    }

    public async Task<WorkItemDetail> UpdateAsync(Guid id, UpdateWorkItemRequest request, CancellationToken cancellationToken = default)
    {
        var item = await LoadScopedAsync(id, cancellationToken);

        if (request.Title is not null)
        {
            if (!_currentUser.CanMutateWorkItems)
            {
                throw new ForbiddenException("Your role cannot edit documents.");
            }

            item.Title = request.Title.Trim();
        }

        if (request.DocumentTypeId is { } typeId)
        {
            if (!_currentUser.CanMutateWorkItems)
            {
                throw new ForbiddenException("Your role cannot edit documents.");
            }

            if (!await _db.DocumentTypes.AnyAsync(x => x.Id == typeId, cancellationToken))
            {
                throw new ValidationException("Document type is not valid.");
            }

            item.DocumentTypeId = typeId;
        }

        string? previousStatusName = null;
        string? nextStatusName = null;
        if (request.StatusId is { } statusId)
        {
            if (!_currentUser.CanMutateWorkItems)
            {
                throw new ForbiddenException("Your role cannot change status.");
            }

            var nextStatusRow = await _db.WorkItemStatuses.AsNoTracking()
                .FirstOrDefaultAsync(x => x.Id == statusId, cancellationToken)
                ?? throw new ValidationException("Status is not valid.");

            if (item.StatusId != statusId)
            {
                previousStatusName = StatusDisplay.Label(item.Status.Name);
                nextStatusName = StatusDisplay.Label(nextStatusRow.Name);
            }

            item.StatusId = statusId;
        }

        if (request.ClearAssignment || request.AssignedToUserId is not null)
        {
            if (!_currentUser.CanMutateWorkItems)
            {
                throw new ForbiddenException("Your role cannot assign work items.");
            }

            if (request.ClearAssignment)
            {
                item.AssignedToUserId = null;
            }
            else if (request.AssignedToUserId is { } assignee)
            {
                await EnsureAssignableAsync(assignee, item.OrganizationId, cancellationToken);
                item.AssignedToUserId = assignee;
            }
        }

        if (request.IsSplit is { } split)
        {
            EnsureCanMutate();
            item.IsSplit = split;
        }

        if (request.IsSketch is { } sketch)
        {
            EnsureCanMutate();
            item.IsSketch = sketch;
        }

        if (request.ClearWorkedOn)
        {
            EnsureCanMutate();
            item.SetWorkedOn(null);
        }
        else if (request.WorkedOn is { } workedOn)
        {
            EnsureCanMutate();
            item.SetWorkedOn(workedOn);
        }

        if (request.ClearFirstDeadline)
        {
            EnsureCanMutate();
            item.SetFirstDeadline(null);
        }
        else if (request.FirstDeadline is { } first)
        {
            EnsureCanMutate();
            item.SetFirstDeadline(first);
        }

        if (request.ClearFinalDeadline)
        {
            EnsureCanMutate();
            item.SetFinalDeadline(null);
        }
        else if (request.FinalDeadline is { } final)
        {
            EnsureCanMutate();
            item.SetFinalDeadline(final);
        }

        if (request.AnnexationCount is { } annex)
        {
            EnsureCanMutate();
            item.AnnexationCount = Math.Max(0, annex);
        }

        if (request.CorrectionCount is { } corrections)
        {
            EnsureCanMutate();
            item.CorrectionCount = Math.Max(0, corrections);
        }

        if (request.DeedCount is { } deeds)
        {
            EnsureCanMutate();
            item.DeedCount = Math.Max(0, deeds);
        }

        if (request.PlatCount is { } plats)
        {
            EnsureCanMutate();
            item.PlatCount = Math.Max(0, plats);
        }

        if (request.IsPriority is { } priority)
        {
            if (!_currentUser.CanMutateWorkItems)
            {
                throw new ForbiddenException("Your role cannot mark priority work.");
            }

            item.ApplyPriority(priority, request.PriorityNote, _currentUser.UserId, DateTimeOffset.UtcNow);
            ApplyNeededBy(item, request);
            if (priority)
            {
                await _notifications.NotifyPriorityAsync(item, _currentUser.UserId, cancellationToken);
            }
        }
        else if (request.PriorityNote is not null && item.IsPriority)
        {
            if (!_currentUser.CanMutateWorkItems)
            {
                throw new ForbiddenException("Your role cannot mark priority work.");
            }

            item.ApplyPriority(true, request.PriorityNote, item.PriorityRequestedByUserId ?? _currentUser.UserId, item.PriorityRequestedAt ?? DateTimeOffset.UtcNow);
            ApplyNeededBy(item, request);
        }
        else if (request.PriorityNeededBy is not null || request.ClearPriorityNeededBy)
        {
            if (!_currentUser.CanMutateWorkItems)
            {
                throw new ForbiddenException("Your role cannot mark priority work.");
            }

            ApplyNeededBy(item, request);
            if (item.IsPriority)
            {
                await _notifications.NotifyPriorityAsync(item, _currentUser.UserId, cancellationToken);
            }
        }

        if (request.IsReviewed is { } reviewed)
        {
            EnsureCanMutate();
            item.IsReviewed = reviewed;
        }

        if (request.PropertyIds is not null)
        {
            EnsureCanMutate();
            item.PropertyIds = string.IsNullOrWhiteSpace(request.PropertyIds) ? null : request.PropertyIds.Trim();
        }

        if (request.StatusId is { } nextStatus &&
            (nextStatus == SeedIds.StatusWorked || nextStatus == SeedIds.StatusQcd) &&
            item.WorkedOn is null)
        {
            item.SetWorkedOn(DateTimeOffset.UtcNow);
        }

        if (request.InternalNotes is not null)
        {
            if (!_currentUser.CanEditInternalNotes)
            {
                throw new ForbiddenException("Your role cannot edit Internal Notes.");
            }

            var next = request.InternalNotes;
            if (!string.Equals(item.InternalNotes ?? string.Empty, next, StringComparison.Ordinal))
            {
                item.InternalNotes = next;
                var editedAt = DateTimeOffset.UtcNow;
                _db.InternalNoteRevisions.Add(new InternalNoteRevision
                {
                    Id = Guid.NewGuid(),
                    WorkItemId = item.Id,
                    Body = next,
                    EditedByUserId = _currentUser.UserId,
                    EditedAt = editedAt,
                    EditedAtSort = editedAt.ToUnixTimeMilliseconds()
                });
            }
        }

        item.UpdatedAt = DateTimeOffset.UtcNow;
        item.UpdatedAtSort = item.UpdatedAt.ToUnixTimeMilliseconds();
        item.UpdatedByUserId = _currentUser.UserId;
        await _db.SaveChangesAsync(cancellationToken);

        if (previousStatusName is not null && nextStatusName is not null)
        {
            try
            {
                await _comms.NotifyStatusChangedAsync(item, previousStatusName, nextStatusName, cancellationToken);
            }
            catch (Exception)
            {
                // Fail closed: status is already saved.
            }
        }

        return MapDetail(await LoadScopedAsync(id, cancellationToken));
    }

    private static void ApplyNeededBy(WorkItem item, UpdateWorkItemRequest request)
    {
        if (request.ClearPriorityNeededBy)
        {
            item.SetPriorityNeededBy(null);
            return;
        }

        if (request.PriorityNeededBy is { } neededBy)
        {
            item.SetPriorityNeededBy(neededBy);
        }
    }

    public async Task<FileDownload> OpenFileAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var item = await LoadScopedAsync(id, cancellationToken);
        if (string.IsNullOrWhiteSpace(item.BlobPath))
        {
            throw new NotFoundException("File was not found.");
        }

        var stream = await _storage.OpenReadAsync(item.BlobPath, cancellationToken);
        return new FileDownload(stream, item.ContentType ?? "application/octet-stream", item.FileName);
    }

    public async Task<FilePreview> OpenPreviewAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var item = await LoadScopedAsync(id, cancellationToken);
        if (string.IsNullOrWhiteSpace(item.BlobPath))
        {
            throw new NotFoundException("File was not found.");
        }

        var stream = await _storage.OpenReadAsync(item.BlobPath, cancellationToken);
        var contentType = item.ContentType ?? "application/octet-stream";

        if (DocumentPreview.IsTiff(item.FileName, contentType))
        {
            try
            {
                return await TiffFirstPagePreview.RasterizeAsync(stream, cancellationToken);
            }
            finally
            {
                await stream.DisposeAsync();
            }
        }

        if (DocumentPreview.IsBrowserImage(item.FileName, contentType))
        {
            return new FilePreview(
                stream,
                contentType,
                item.FileName,
                1,
                false,
                DocumentPreview.BrowserImageKind);
        }

        await stream.DisposeAsync();
        throw new ValidationException(DocumentPreview.UnavailableMessage);
    }

    public async Task<DashboardResponse> GetDashboardAsync(DashboardQuery query, CancellationToken cancellationToken = default)
    {
        await EnsureOrganizationFilterAsync(query.OrganizationId, cancellationToken);
        if (!Roles.CanSeeDashboardAssignee(_currentUser.Role))
        {
            query.AssignedToUserId = null;
            query.UnassignedOnly = false;
        }

        var allowed = await _orgScope.GetAllowedOrganizationIdsAsync(cancellationToken);
        var items = BaseQuery(allowed);
        if (query.OrganizationId is { } orgId)
        {
            items = items.Where(x => x.OrganizationId == orgId);
        }

        if (query.UnassignedOnly)
        {
            items = items.Where(x => x.AssignedToUserId == null);
        }
        else if (query.AssignedToUserId is { } assigned)
        {
            items = items.Where(x => x.AssignedToUserId == assigned);
        }

        // CR03: Active is the current-state queue for this org + assignee scope —
        // the same filters as Manage Documents status=Active. Do not apply the
        // dashboard status dropdown or date window; those made the tile disagree
        // with the destination list (older Active items and pagination totals).
        var active = await items.CountAsync(x => x.StatusId == SeedIds.StatusInProgress, cancellationToken);

        if (query.StatusId is { } statusId)
        {
            items = items.Where(x => x.StatusId == statusId);
        }

        var range = DashboardRange.Resolve(query.From, query.To);
        var fromSort = range.FromSort;
        var toSort = range.ToSort;
        var window = items.Where(x =>
            (x.UploadedAtSort >= fromSort && x.UploadedAtSort <= toSort) ||
            (x.WorkedOnSort != null && x.WorkedOnSort >= fromSort && x.WorkedOnSort <= toSort));

        var pending = await window.CountAsync(x => x.StatusId == SeedIds.StatusPending, cancellationToken);
        var priority = await window.CountAsync(x => x.IsPriority, cancellationToken);
        var completed = await items.CountAsync(
            x => (x.StatusId == SeedIds.StatusWorked || x.StatusId == SeedIds.StatusQcd) &&
                 (x.WorkedOnSort ?? x.UpdatedAtSort) >= fromSort &&
                 (x.WorkedOnSort ?? x.UpdatedAtSort) <= toSort,
            cancellationToken);

        var statusCounts = await window
            .GroupBy(x => new { x.StatusId, x.Status.Name, x.Status.Color, x.Status.SortOrder })
            .Select(g => new { g.Key.StatusId, g.Key.Name, g.Key.Color, g.Key.SortOrder, Count = g.Count() })
            .OrderBy(x => x.SortOrder)
            .ToListAsync(cancellationToken);

        // CR10 — CAD and technician document counts use created/uploaded in range
        // (not the uploaded-or-worked KPI window) so they reconcile with Manage Documents.
        var uploadedWindow = items.Where(x => x.UploadedAtSort >= fromSort && x.UploadedAtSort <= toSort);

        var assigneeRows = await uploadedWindow
            .Where(x => x.AssignedToUserId != null)
            .GroupBy(x => new
            {
                x.AssignedToUserId,
                Name = x.AssignedToUser!.FullName != null && x.AssignedToUser.FullName != ""
                    ? x.AssignedToUser.FullName
                    : x.AssignedToUser.DisplayName,
                IsArchived = x.AssignedToUser.IsArchived
            })
            .Select(g => new { g.Key.AssignedToUserId, g.Key.Name, g.Key.IsArchived, Count = g.Count() })
            .OrderByDescending(x => x.Count)
            .ToListAsync(cancellationToken);

        var unassignedCount = await uploadedWindow.CountAsync(x => x.AssignedToUserId == null, cancellationToken);

        var orgCounts = await uploadedWindow
            .GroupBy(x => new { x.OrganizationId, x.Organization.Name })
            .Select(g => new { g.Key.OrganizationId, g.Key.Name, Count = g.Count() })
            .OrderBy(x => x.Name)
            .ToListAsync(cancellationToken);

        var technicianCounts = DocumentVolume.WithUnassignedBucket(
            assigneeRows.Select(x => new NamedCount(
                x.AssignedToUserId!.Value,
                UserIdentity.WithArchivedSuffix(x.Name, x.IsArchived),
                null,
                x.Count)),
            unassignedCount);

        var recentRows = await items
            .Where(x =>
                (x.StatusId == SeedIds.StatusWorked || x.StatusId == SeedIds.StatusQcd) &&
                (x.WorkedOnSort ?? x.UpdatedAtSort) >= fromSort &&
                (x.WorkedOnSort ?? x.UpdatedAtSort) <= toSort)
            .OrderByDescending(x => x.WorkedOnSort ?? x.UpdatedAtSort)
            .Take(8)
            .Select(x => new
            {
                x.Id,
                x.FileName,
                x.Title,
                x.OrganizationId,
                OrganizationName = x.Organization.Name,
                x.DocumentTypeId,
                DocumentTypeName = x.DocumentType.Name,
                x.StatusId,
                StatusName = x.Status.Name,
                StatusColor = x.Status.Color,
                AssignedToName = x.AssignedToUser != null
                    ? (x.AssignedToUser.FullName != null && x.AssignedToUser.FullName != ""
                        ? x.AssignedToUser.FullName
                        : x.AssignedToUser.DisplayName)
                    : null,
                AssignedToIsArchived = x.AssignedToUser != null && x.AssignedToUser.IsArchived,
                x.AssignedToUserId,
                x.PriorityNeededBy,
                x.UploadedAt,
                UploadedByName = x.UploadedByUser.FullName != null && x.UploadedByUser.FullName != ""
                    ? x.UploadedByUser.FullName
                    : x.UploadedByUser.DisplayName,
                UploadedByIsArchived = x.UploadedByUser.IsArchived,
                x.UpdatedAt,
                x.WorkedOn,
                Minutes = x.TimeEntries.Sum(t => t.Minutes),
                x.FirstDeadline,
                x.FinalDeadline,
                x.ContentType,
                x.FileSizeBytes,
                x.IsPriority,
                x.PriorityNote,
                x.IsReviewed
            })
            .ToListAsync(cancellationToken);

        return new DashboardResponse(
            [
                new DashboardKpi("active", "Active", active, "#1890ff"),
                new DashboardKpi("pending", "Pending", pending, "#faad14"),
                new DashboardKpi("completed", "Completed", completed, "#52c41a"),
                new DashboardKpi("priority", "Priority", priority, "#f5222d")
            ],
            statusCounts
                .Where(x => SeedIds.IsCanonicalStatus(x.StatusId))
                .Select(x => new NamedCount(x.StatusId, StatusDisplay.Label(x.Name), x.Color, x.Count))
                .ToList(),
            technicianCounts,
            orgCounts.Select(x => new NamedCount(x.OrganizationId, x.Name, null, x.Count)).ToList(),
            recentRows.Select(x => new WorkItemListItem(
                x.Id, x.FileName, x.Title, x.OrganizationId, x.OrganizationName, x.DocumentTypeId, x.DocumentTypeName,
                x.StatusId, StatusDisplay.Label(x.StatusName), x.StatusColor, UserIdentity.WithArchivedSuffix(x.AssignedToName, x.AssignedToIsArchived), x.AssignedToUserId, x.PriorityNeededBy,
                x.UploadedAt, UserIdentity.WithArchivedSuffix(x.UploadedByName, x.UploadedByIsArchived), x.UpdatedAt,
                x.WorkedOn, TimeDurations.ToHours(x.Minutes), TimeDurations.Format(x.Minutes), x.FirstDeadline, x.FinalDeadline,
                x.ContentType, x.FileSizeBytes, x.IsPriority, x.PriorityNote, x.IsReviewed)).ToList(),
            await BuildVolumeOverTimeAsync(items, range, cancellationToken),
            await BuildHoursByAssigneeAsync(items, fromSort, toSort, cancellationToken),
            await BuildHoursByClientAsync(items, fromSort, toSort, cancellationToken),
            range.From,
            range.To,
            range.Label);
    }

    public async Task<PublicUploadInfo> GetPublicUploadAsync(string token, CancellationToken cancellationToken = default)
    {
        var org = await FindOrgByTokenAsync(token, cancellationToken);
        var types = await _db.DocumentTypes.AsNoTracking()
            .OrderBy(x => x.SortOrder)
            .Select(x => new LookupItem(x.Id, x.Name, null, x.SortOrder))
            .ToListAsync(cancellationToken);
        return new PublicUploadInfo(
            org.Name,
            types,
            _uploads.EffectiveMaxFileBytes,
            _uploads.EffectiveMaxFileMegabytes,
            _uploads.EffectiveConcurrency,
            await AssignedTechnicianDisplayAsync(org.Id, cancellationToken),
            UploadFileTypes.Extensions,
            UploadFileTypes.SupportedTypesLabel,
            UploadFileTypes.AcceptAttribute);
    }

    public async Task<PublicUploadResult> UploadByTokenAsync(string token, UploadWorkItemRequest request, CancellationToken cancellationToken = default)
    {
        var org = await FindOrgByTokenAsync(token, cancellationToken);
        var documentTypeId = await ResolveDocumentTypeAsync(request.DocumentTypeId, request.DocumentTypeName, cancellationToken);
        if (string.IsNullOrWhiteSpace(request.FileName))
        {
            throw new ValidationException("A file is required.");
        }

        EnsureWithinSizeLimit(request);

        if (!UploadFileTypes.TryResolve(request.FileName, request.ContentType, out var contentType))
        {
            throw new ValidationException(UploadFileTypes.UnsupportedFileMessage(request.FileName));
        }

        if (!await _db.Users.AnyAsync(x => x.Id == SeedIds.TokenUploadUser, cancellationToken))
        {
            throw new ValidationException("Token upload is not available yet.");
        }

        var assignee = await ResolveDefaultAssigneeAsync(org.Id, cancellationToken);
        if (assignee is { } tokenAssignee)
        {
            await EnsureAssignableAsync(tokenAssignee, org.Id, cancellationToken);
        }

        var now = DateTimeOffset.UtcNow;
        var item = new WorkItem
        {
            Id = Guid.NewGuid(),
            OrganizationId = org.Id,
            FileName = Path.GetFileName(request.FileName),
            Title = string.IsNullOrWhiteSpace(request.Title) ? Path.GetFileNameWithoutExtension(request.FileName) : request.Title.Trim(),
            DocumentTypeId = documentTypeId,
            StatusId = SeedIds.StatusPending,
            AssignedToUserId = assignee,
            UploadedByUserId = SeedIds.TokenUploadUser,
            UpdatedByUserId = SeedIds.TokenUploadUser,
            ContentType = contentType
        };
        item.TouchDates(now, now);

        var blobPath = await _storage.SaveAsync(
            item.OrganizationId,
            item.Id,
            item.FileName,
            request.Content,
            contentType,
            cancellationToken);

        item.BlobPath = blobPath;
        item.FileSizeBytes = request.Content.CanSeek ? request.Content.Length : 0;
        var becamePriority = false;
        if (request.IsPriority)
        {
            becamePriority = item.ApplyPriority(true, request.PriorityNote, SeedIds.TokenUploadUser, now);
        }

        _db.WorkItems.Add(item);
        AddClientNotesComment(item.Id, request.ClientNotes, SeedIds.TokenUploadUser);
        await _db.SaveChangesAsync(cancellationToken);
        if (becamePriority)
        {
            await _notifications.NotifyPriorityAsync(item, SeedIds.TokenUploadUser, cancellationToken);
        }

        return new PublicUploadResult(item.Id, item.FileName, org.Name, "Pending");
    }

    public async Task NotifyPublicUploadReceivedAsync(
        string token,
        PublicUploadReceivedRequest request,
        CancellationToken cancellationToken = default)
    {
        var org = await FindOrgByTokenAsync(token, cancellationToken);
        var names = (request.FileNames ?? [])
            .Where(x => !string.IsNullOrWhiteSpace(x))
            .Select(x => x.Trim())
            .ToList();
        try
        {
            await _comms.NotifyPublicUploadReceivedAsync(org, request.FileCount, names, cancellationToken);
        }
        catch (Exception)
        {
            // Fail closed: uploads already succeeded.
        }
    }

    public async Task<ExcelExport> ExportAsync(WorkItemQuery query, CancellationToken cancellationToken = default)
    {
        await EnsureOrganizationFilterAsync(query.OrganizationId, cancellationToken);
        var allowed = await _orgScope.GetAllowedOrganizationIdsAsync(cancellationToken);
        var filtered = ApplyBucket(ApplyFilters(BaseQuery(allowed), query), query.Bucket);
        var rows = await ApplySort(filtered, query)
            .Take(5000)
            .Select(x => new
            {
                x.Id,
                x.FileName,
                x.Title,
                x.OrganizationId,
                OrganizationName = x.Organization.Name,
                x.DocumentTypeId,
                DocumentTypeName = x.DocumentType.Name,
                x.StatusId,
                StatusName = x.Status.Name,
                StatusColor = x.Status.Color,
                AssignedToName = x.AssignedToUser != null
                    ? (x.AssignedToUser.FullName != null && x.AssignedToUser.FullName != ""
                        ? x.AssignedToUser.FullName
                        : x.AssignedToUser.DisplayName)
                    : null,
                AssignedToIsArchived = x.AssignedToUser != null && x.AssignedToUser.IsArchived,
                x.AssignedToUserId,
                x.PriorityNeededBy,
                x.UploadedAt,
                UploadedByName = x.UploadedByUser.FullName != null && x.UploadedByUser.FullName != ""
                    ? x.UploadedByUser.FullName
                    : x.UploadedByUser.DisplayName,
                UploadedByIsArchived = x.UploadedByUser.IsArchived,
                x.UpdatedAt,
                x.WorkedOn,
                Minutes = x.TimeEntries.Sum(t => t.Minutes),
                x.FirstDeadline,
                x.FinalDeadline,
                x.ContentType,
                x.FileSizeBytes,
                x.IsPriority,
                x.PriorityNote,
                x.IsReviewed
            })
            .ToListAsync(cancellationToken);

        var items = rows.Select(x => new WorkItemListItem(
            x.Id, x.FileName, x.Title, x.OrganizationId, x.OrganizationName, x.DocumentTypeId, x.DocumentTypeName,
            x.StatusId, StatusDisplay.Label(x.StatusName), x.StatusColor, UserIdentity.WithArchivedSuffix(x.AssignedToName, x.AssignedToIsArchived), x.AssignedToUserId, x.PriorityNeededBy,
            x.UploadedAt, UserIdentity.WithArchivedSuffix(x.UploadedByName, x.UploadedByIsArchived), x.UpdatedAt,
            x.WorkedOn, TimeDurations.ToHours(x.Minutes), TimeDurations.Format(x.Minutes), x.FirstDeadline, x.FinalDeadline,
            x.ContentType, x.FileSizeBytes, x.IsPriority, x.PriorityNote, x.IsReviewed)).ToList();
        return WorkItemCsvExport.Build(items);
    }

    private async Task<Organization> FindOrgByTokenAsync(string token, CancellationToken cancellationToken)
    {
        if (!Security.UploadTokens.IsValidFormat(token))
        {
            throw new NotFoundException("Upload link was not found.");
        }

        var value = token.Trim();
        var org = await _db.Organizations.FirstOrDefaultAsync(x => x.UploadToken == value, cancellationToken);
        if (org is null || !org.IsActive)
        {
            throw new NotFoundException("Upload link was not found.");
        }

        return org;
    }

    private static async Task<IReadOnlyList<DayVolume>> BuildVolumeOverTimeAsync(
        IQueryable<WorkItem> items,
        DashboardRange.Resolved range,
        CancellationToken cancellationToken)
    {
        var fromSort = range.FromSort;
        var toSort = range.ToSort;
        var rows = await items
            .Where(x =>
                (x.UploadedAtSort >= fromSort && x.UploadedAtSort <= toSort) ||
                (x.WorkedOnSort != null && x.WorkedOnSort >= fromSort && x.WorkedOnSort <= toSort))
            .Select(x => new { x.UploadedAt, x.WorkedOn, x.StatusId })
            .ToListAsync(cancellationToken);

        return Enumerable.Range(0, range.DayCount).Select(offset =>
        {
            var day = range.StartDate.AddDays(offset);
            var uploaded = rows.Count(x => x.UploadedAt.UtcDateTime.Date == day);
            var completed = rows.Count(x =>
                x.WorkedOn is { } worked &&
                worked.UtcDateTime.Date == day &&
                (x.StatusId == SeedIds.StatusWorked || x.StatusId == SeedIds.StatusQcd));
            return new DayVolume(day.ToString("yyyy-MM-dd"), uploaded, completed);
        }).ToList();
    }

    private static async Task<IReadOnlyList<HoursSlice>> BuildHoursByAssigneeAsync(
        IQueryable<WorkItem> items,
        long fromSort,
        long toSort,
        CancellationToken cancellationToken)
    {
        var rows = await items
            .Select(x => new
            {
                Id = x.AssignedToUserId ?? Guid.Empty,
                Name = x.AssignedToUser != null
                    ? (x.AssignedToUser.FullName != null && x.AssignedToUser.FullName != ""
                        ? x.AssignedToUser.FullName
                        : x.AssignedToUser.DisplayName)
                    : "Unassigned",
                IsArchived = x.AssignedToUser != null && x.AssignedToUser.IsArchived,
                Minutes = x.TimeEntries
                    .Where(t => t.WorkedOnSort >= fromSort && t.WorkedOnSort <= toSort)
                    .Sum(t => t.Minutes)
            })
            .ToListAsync(cancellationToken);

        return rows
            .GroupBy(x => new { x.Id, Name = UserIdentity.WithArchivedSuffix(x.Name, x.IsArchived) })
            .Select(g => new HoursSlice(g.Key.Id, g.Key.Name, TimeDurations.ToHours(g.Sum(x => x.Minutes))))
            .Where(x => x.Hours > 0)
            .OrderByDescending(x => x.Hours)
            .Take(8)
            .ToList();
    }

    private static async Task<IReadOnlyList<HoursSlice>> BuildHoursByClientAsync(
        IQueryable<WorkItem> items,
        long fromSort,
        long toSort,
        CancellationToken cancellationToken)
    {
        var rows = await items
            .Select(x => new
            {
                x.OrganizationId,
                Name = x.Organization.Name,
                Minutes = x.TimeEntries
                    .Where(t => t.WorkedOnSort >= fromSort && t.WorkedOnSort <= toSort)
                    .Sum(t => t.Minutes)
            })
            .ToListAsync(cancellationToken);

        return rows
            .GroupBy(x => new { x.OrganizationId, x.Name })
            .Select(g => new HoursSlice(g.Key.OrganizationId, g.Key.Name, TimeDurations.ToHours(g.Sum(x => x.Minutes))))
            .Where(x => x.Hours > 0)
            .OrderByDescending(x => x.Hours)
            .Take(8)
            .ToList();
    }

    private Task EnsureOrganizationFilterAsync(Guid? organizationId, CancellationToken cancellationToken) =>
        organizationId is { } orgId
            ? _orgScope.EnsureCanAccessOrganizationAsync(orgId, cancellationToken)
            : Task.CompletedTask;

    private IQueryable<WorkItem> BaseQuery(IReadOnlyCollection<Guid> allowed) =>
        _db.WorkItems.AsNoTracking().Where(x => allowed.Contains(x.OrganizationId));

    private IQueryable<WorkItem> ApplyFilters(IQueryable<WorkItem> query, WorkItemQuery filter, bool ignoreAssignee = false)
    {
        if (filter.OrganizationId is { } orgId)
        {
            query = query.Where(x => x.OrganizationId == orgId);
        }

        if (filter.DocumentTypeId is { } typeId)
        {
            query = query.Where(x => x.DocumentTypeId == typeId);
        }

        if (filter.StatusId is { } statusId)
        {
            query = query.Where(x => x.StatusId == statusId);
        }

        if (!ignoreAssignee && Roles.CanSeeDashboardAssignee(_currentUser.Role))
        {
            if (filter.UnassignedOnly)
            {
                query = query.Where(x => x.AssignedToUserId == null);
            }
            else if (filter.AssignedToUserId is { } assigned)
            {
                query = query.Where(x => x.AssignedToUserId == assigned);
            }
        }

        if (filter.UploadedFrom is { } from)
        {
            var fromSort = from.ToUnixTimeMilliseconds();
            query = query.Where(x => x.UploadedAtSort >= fromSort);
        }

        if (filter.UploadedTo is { } to)
        {
            var toSort = to.ToUnixTimeMilliseconds();
            query = query.Where(x => x.UploadedAtSort <= toSort);
        }

        if (filter.WorkedFrom is { } workedFrom)
        {
            var fromSort = workedFrom.ToUnixTimeMilliseconds();
            query = query.Where(x => x.WorkedOnSort != null && x.WorkedOnSort >= fromSort);
        }

        if (filter.WorkedTo is { } workedTo)
        {
            var toSort = workedTo.ToUnixTimeMilliseconds();
            query = query.Where(x => x.WorkedOnSort != null && x.WorkedOnSort <= toSort);
        }

        if (!string.IsNullOrWhiteSpace(filter.Search))
        {
            var term = filter.Search.Trim();
            var statusNames = StatusDisplay.NamesMatchingSearch(term);
            query = query.Where(x =>
                x.FileName.Contains(term) ||
                x.Title.Contains(term) ||
                x.Organization.Name.Contains(term) ||
                x.DocumentType.Name.Contains(term) ||
                x.Status.Name.Contains(term) ||
                statusNames.Contains(x.Status.Name) ||
                (x.AssignedToUser != null && (
                    x.AssignedToUser.DisplayName.Contains(term) ||
                    (x.AssignedToUser.FullName != null && x.AssignedToUser.FullName.Contains(term)))));
        }

        return query;
    }

    private IQueryable<WorkItem> ApplyBucket(IQueryable<WorkItem> query, string? bucket) =>
        bucket?.ToLowerInvariant() switch
        {
            "pending" => query.Where(x => x.StatusId == SeedIds.StatusPending),
            "mine" => query.Where(x => x.AssignedToUserId == _currentUser.UserId),
            "unassigned" => query.Where(x => x.AssignedToUserId == null),
            "hold" => query.Where(x => x.StatusId == SeedIds.StatusHeld),
            "completed" => query.Where(x => x.StatusId == SeedIds.StatusWorked || x.StatusId == SeedIds.StatusQcd),
            "firstdeadline" => query.Where(x => x.FirstDeadlineSort != null),
            "finaldeadline" => query.Where(x => x.FinalDeadlineSort != null),
            "priority" => query.Where(x => x.IsPriority),
            "duethisweek" => ApplyDueThisWeek(query),
            _ => query
        };

    private async Task<BucketCounts> ComputeBucketsAsync(
        IQueryable<WorkItem> query,
        IQueryable<WorkItem> assigneeAgnostic,
        CancellationToken cancellationToken)
    {
        var pending = await query.CountAsync(x => x.StatusId == SeedIds.StatusPending, cancellationToken);
        var mine = await query.CountAsync(x => x.AssignedToUserId == _currentUser.UserId, cancellationToken);
        var hold = await query.CountAsync(x => x.StatusId == SeedIds.StatusHeld, cancellationToken);
        var completed = await query.CountAsync(x => x.StatusId == SeedIds.StatusWorked || x.StatusId == SeedIds.StatusQcd, cancellationToken);
        var first = await query.CountAsync(x => x.FirstDeadlineSort != null, cancellationToken);
        var final = await query.CountAsync(x => x.FinalDeadlineSort != null, cancellationToken);
        var priority = await query.CountAsync(x => x.IsPriority, cancellationToken);
        var dueThisWeek = await ApplyDueThisWeek(query).CountAsync(cancellationToken);
        // CR08 — Unassigned count stays visible on the personal Assigned-to queue.
        var unassigned = await assigneeAgnostic.CountAsync(x => x.AssignedToUserId == null, cancellationToken);
        return new BucketCounts(pending, mine, hold, completed, first, final, priority, dueThisWeek, unassigned);
    }

    private static IQueryable<WorkItem> ApplyDueThisWeek(IQueryable<WorkItem> query)
    {
        var today = new DateTimeOffset(DateTime.UtcNow.Date, TimeSpan.Zero);
        var start = today.ToUnixTimeMilliseconds();
        var end = today.AddDays(7).ToUnixTimeMilliseconds();
        return query.Where(x =>
            x.IsPriority &&
            x.PriorityNeededBySort != null &&
            x.PriorityNeededBySort >= start &&
            x.PriorityNeededBySort <= end);
    }

    private IQueryable<WorkItem> ApplySort(IQueryable<WorkItem> query, WorkItemQuery filter)
    {
        var desc = !string.Equals(filter.SortDir, "asc", StringComparison.OrdinalIgnoreCase);
        var grouped = filter.GroupBy?.ToLowerInvariant() switch
        {
            "status" => (IOrderedQueryable<WorkItem>)query.OrderBy(x => x.Status.SortOrder),
            "organization" or "client" => (IOrderedQueryable<WorkItem>)query.OrderBy(x => x.Organization.Name),
            "assignedto" => (IOrderedQueryable<WorkItem>)query.OrderBy(x => x.AssignedToUser != null ? x.AssignedToUser.DisplayName : string.Empty),
            "documenttype" => (IOrderedQueryable<WorkItem>)query.OrderBy(x => x.DocumentType.Name),
            _ => null
        };

        return grouped is null
            ? ApplyPrimarySort(query, filter.SortBy, desc)
            : ApplyThenSort(grouped, filter.SortBy, desc);
    }

    /// <summary>
    /// CR05 default order: Pending first, then other statuses, oldest upload/created ascending.
    /// </summary>
    private static IOrderedQueryable<WorkItem> ApplyQueueSort(IQueryable<WorkItem> query) =>
        query
            .OrderBy(x => x.StatusId == SeedIds.StatusPending ? 0 : 1)
            .ThenBy(x => x.Status.SortOrder)
            .ThenBy(x => x.UploadedAtSort);

    private static IOrderedQueryable<WorkItem> ApplyPrimarySort(IQueryable<WorkItem> query, string? sortBy, bool desc) =>
        sortBy?.ToLowerInvariant() switch
        {
            "queue" => ApplyQueueSort(query),
            "filename" or "title" => desc ? query.OrderByDescending(x => x.FileName) : query.OrderBy(x => x.FileName),
            "organization" or "client" => desc ? query.OrderByDescending(x => x.Organization.Name) : query.OrderBy(x => x.Organization.Name),
            "documenttype" => desc ? query.OrderByDescending(x => x.DocumentType.Name) : query.OrderBy(x => x.DocumentType.Name),
            "status" => desc ? query.OrderByDescending(x => x.Status.SortOrder) : query.OrderBy(x => x.Status.SortOrder),
            "assignedto" => desc
                ? query.OrderByDescending(x => x.AssignedToUser != null ? x.AssignedToUser.DisplayName : string.Empty)
                : query.OrderBy(x => x.AssignedToUser != null ? x.AssignedToUser.DisplayName : string.Empty),
            "updatedat" => desc ? query.OrderByDescending(x => x.UpdatedAtSort) : query.OrderBy(x => x.UpdatedAtSort),
            "workedon" or "workedat" => desc ? query.OrderByDescending(x => x.WorkedOnSort) : query.OrderBy(x => x.WorkedOnSort),
            "hours" or "totaltime" or "total" => desc
                ? query.OrderByDescending(x => x.TimeEntries.Sum(t => t.Minutes))
                : query.OrderBy(x => x.TimeEntries.Sum(t => t.Minutes)),
            "reviewed" or "review" => desc
                ? query.OrderByDescending(x => x.IsReviewed)
                : query.OrderBy(x => x.IsReviewed),
            "firstdeadline" => desc ? query.OrderByDescending(x => x.FirstDeadlineSort) : query.OrderBy(x => x.FirstDeadlineSort),
            "finaldeadline" => desc ? query.OrderByDescending(x => x.FinalDeadlineSort) : query.OrderBy(x => x.FinalDeadlineSort),
            _ => desc ? query.OrderByDescending(x => x.UploadedAtSort) : query.OrderBy(x => x.UploadedAtSort)
        };

    private static IOrderedQueryable<WorkItem> ApplyThenSort(IOrderedQueryable<WorkItem> query, string? sortBy, bool desc) =>
        sortBy?.ToLowerInvariant() switch
        {
            "queue" => query
                .ThenBy(x => x.StatusId == SeedIds.StatusPending ? 0 : 1)
                .ThenBy(x => x.Status.SortOrder)
                .ThenBy(x => x.UploadedAtSort),
            "filename" or "title" => desc ? query.ThenByDescending(x => x.FileName) : query.ThenBy(x => x.FileName),
            "organization" or "client" => desc ? query.ThenByDescending(x => x.Organization.Name) : query.ThenBy(x => x.Organization.Name),
            "documenttype" => desc ? query.ThenByDescending(x => x.DocumentType.Name) : query.ThenBy(x => x.DocumentType.Name),
            "status" => desc ? query.ThenByDescending(x => x.Status.SortOrder) : query.ThenBy(x => x.Status.SortOrder),
            "assignedto" => desc
                ? query.ThenByDescending(x => x.AssignedToUser != null ? x.AssignedToUser.DisplayName : string.Empty)
                : query.ThenBy(x => x.AssignedToUser != null ? x.AssignedToUser.DisplayName : string.Empty),
            "updatedat" => desc ? query.ThenByDescending(x => x.UpdatedAtSort) : query.ThenBy(x => x.UpdatedAtSort),
            "workedon" or "workedat" => desc ? query.ThenByDescending(x => x.WorkedOnSort) : query.ThenBy(x => x.WorkedOnSort),
            "hours" or "totaltime" or "total" => desc
                ? query.ThenByDescending(x => x.TimeEntries.Sum(t => t.Minutes))
                : query.ThenBy(x => x.TimeEntries.Sum(t => t.Minutes)),
            "reviewed" or "review" => desc
                ? query.ThenByDescending(x => x.IsReviewed)
                : query.ThenBy(x => x.IsReviewed),
            "firstdeadline" => desc ? query.ThenByDescending(x => x.FirstDeadlineSort) : query.ThenBy(x => x.FirstDeadlineSort),
            "finaldeadline" => desc ? query.ThenByDescending(x => x.FinalDeadlineSort) : query.ThenBy(x => x.FinalDeadlineSort),
            _ => desc ? query.ThenByDescending(x => x.UploadedAtSort) : query.ThenBy(x => x.UploadedAtSort)
        };

    private void EnsureCanMutate()
    {
        if (!_currentUser.CanMutateWorkItems)
        {
            throw new ForbiddenException("Your role cannot edit documents.");
        }
    }

    private async Task<WorkItem> LoadScopedAsync(Guid id, CancellationToken cancellationToken)
    {
        var allowed = await _orgScope.GetAllowedOrganizationIdsAsync(cancellationToken);
        var item = await _db.WorkItems
            .Include(x => x.Organization)
            .Include(x => x.DocumentType)
            .Include(x => x.Status)
            .Include(x => x.AssignedToUser)
            .Include(x => x.UploadedByUser)
            .Include(x => x.NoteRevisions)
                .ThenInclude(x => x.EditedByUser)
            .Include(x => x.TimeEntries)
            .FirstOrDefaultAsync(x => x.Id == id && allowed.Contains(x.OrganizationId), cancellationToken);

        if (item is null)
        {
            throw new NotFoundException("Document was not found.");
        }

        return item;
    }

    private async Task EnsureAssignableAsync(Guid userId, Guid organizationId, CancellationToken cancellationToken)
    {
        var user = await _db.Users.FirstOrDefaultAsync(x => x.Id == userId && x.IsActive && !x.IsArchived, cancellationToken)
            ?? throw new ValidationException("Assigned user was not found.");

        var roles = await _db.UserRoles
            .Where(x => x.UserId == userId)
            .Join(_db.Roles, ur => ur.RoleId, r => r.Id, (_, r) => r.Name)
            .ToListAsync(cancellationToken);

        if (roles.Any(role => role is not null && Roles.IsOrgScopedClient(role))
            || !roles.Any(role => role is not null && Roles.CanBeAssignedWork(role)))
        {
            throw new ValidationException("Work items can only be assigned to Administrators or Editors.");
        }

        if (!roles.Any(role => role is not null && Roles.CanSeeAllOrganizations(role)))
        {
            var assigned = await _db.UserOrganizations.AnyAsync(
                x => x.UserId == user.Id && x.OrganizationId == organizationId,
                cancellationToken);
            if (!assigned)
            {
                throw new ValidationException("Assigned user is not on this organization.");
            }
        }
    }

    private void AddClientNotesComment(Guid workItemId, string? notes, Guid authorUserId)
    {
        var body = (notes ?? string.Empty).Trim();
        if (string.IsNullOrWhiteSpace(body))
        {
            return;
        }

        if (body.Length > 2000)
        {
            body = body[..2000];
        }

        var now = DateTimeOffset.UtcNow;
        _db.WorkItemComments.Add(new WorkItemComment
        {
            Id = Guid.NewGuid(),
            WorkItemId = workItemId,
            AuthorUserId = authorUserId,
            Body = body,
            CreatedAt = now,
            CreatedAtSort = now.ToUnixTimeMilliseconds()
        });
    }

    private async Task<IReadOnlyList<AssignedTechnicianDisplay>> AssignedTechnicianDisplayAsync(
        Guid organizationId,
        CancellationToken cancellationToken)
    {
        var rows = await _db.OrganizationTechs.AsNoTracking()
            .Where(x => x.OrganizationId == organizationId)
            .Select(x => new
            {
                x.User.FullName,
                x.User.DisplayName,
                x.User.UserName,
                x.User.Email,
                x.User.IsArchived,
                x.IsPrimary
            })
            .ToListAsync(cancellationToken);
        return AssignedTechnicianNames.FromUsers(
            rows.Select(x => (x.FullName, x.DisplayName, x.UserName, (string?)x.Email, x.IsPrimary, x.IsArchived)));
    }

    private async Task<Guid?> ResolveDefaultAssigneeAsync(Guid organizationId, CancellationToken cancellationToken)
    {
        var rows = await _db.OrganizationTechs.AsNoTracking()
            .Where(tech => tech.OrganizationId == organizationId)
            .Select(tech => new
            {
                tech.UserId,
                tech.User.DisplayName,
                tech.IsPrimary,
                tech.User.IsActive,
                tech.User.IsArchived
            })
            .ToListAsync(cancellationToken);

        return OrgAssignee.ResolveDefault(rows.Select(x => (
            x.UserId,
            x.DisplayName,
            x.IsPrimary,
            UserIdentity.CanSignIn(x.IsActive, x.IsArchived))));
    }

    private WorkItemDetail MapDetail(WorkItem item)
    {
        IReadOnlyList<NoteRevisionDto> history = [];
        DateTimeOffset? notesUpdatedAt = null;
        string? notesUpdatedBy = null;
        if (_currentUser.CanSeeInternalNotes)
        {
            history = item.NoteRevisions
                .OrderByDescending(x => x.EditedAtSort)
                .Select(x => new NoteRevisionDto(
                    x.Id,
                    x.Body,
                    x.EditedAt,
                    UserIdentity.HistoricalName(
                        x.EditedByUser.FullName,
                        x.EditedByUser.DisplayName,
                        x.EditedByUser.IsArchived,
                        x.EditedByUser.UserName,
                        x.EditedByUser.Email)))
                .ToList();
            var latest = history.FirstOrDefault();
            notesUpdatedAt = latest?.EditedAt;
            notesUpdatedBy = latest?.EditedByName;
        }

        var minutes = item.TimeEntries.Sum(x => x.Minutes);
        return new WorkItemDetail(
            item.Id,
            item.FileName,
            item.Title,
            item.OrganizationId,
            item.Organization.Name,
            item.DocumentTypeId,
            item.DocumentType.Name,
            item.StatusId,
            StatusDisplay.Label(item.Status.Name),
            item.Status.Color,
            item.AssignedToUserId,
            item.AssignedToUser is null
                ? null
                : UserIdentity.HistoricalName(
                    item.AssignedToUser.FullName,
                    item.AssignedToUser.DisplayName,
                    item.AssignedToUser.IsArchived,
                    item.AssignedToUser.UserName,
                    item.AssignedToUser.Email),
            _currentUser.CanSeeInternalNotes ? item.InternalNotes : null,
            _currentUser.CanSeeInternalNotes,
            _currentUser.CanEditInternalNotes,
            notesUpdatedAt,
            notesUpdatedBy,
            history,
            _currentUser.CanSeeTimeLogs,
            _currentUser.CanLogTime,
            _currentUser.CanMutateWorkItems,
            _currentUser.CanPostComments,
            item.IsSplit,
            item.IsSketch,
            item.WorkedOn,
            TimeDurations.ToHours(minutes),
            TimeDurations.Format(minutes),
            item.FirstDeadline,
            item.FinalDeadline,
            item.AnnexationCount,
            item.CorrectionCount,
            item.DeedCount,
            item.PlatCount,
            item.PropertyIds,
            item.UploadedAt,
            UserIdentity.HistoricalName(
                item.UploadedByUser.FullName,
                item.UploadedByUser.DisplayName,
                item.UploadedByUser.IsArchived,
                item.UploadedByUser.UserName,
                item.UploadedByUser.Email),
            item.UpdatedAt,
            item.ContentType,
            item.FileSizeBytes,
            item.IsPriority,
            item.PriorityNote,
            item.PriorityRequestedAt,
            item.PriorityRequestedByUserId == SeedIds.TokenUploadUser ? "Upload link" : null,
            _currentUser.CanMutateWorkItems,
            item.IsReviewed,
            item.PriorityNeededBy);
    }

    private async Task<Guid> ResolveDocumentTypeAsync(
        Guid documentTypeId,
        string? documentTypeName,
        CancellationToken cancellationToken,
        bool allowDefault = false)
    {
        if (documentTypeId != Guid.Empty &&
            await _db.DocumentTypes.AnyAsync(x => x.Id == documentTypeId, cancellationToken))
        {
            return documentTypeId;
        }

        if (!string.IsNullOrWhiteSpace(documentTypeName))
        {
            var key = documentTypeName.Trim().ToLowerInvariant();
            var match = await _db.DocumentTypes.AsNoTracking()
                .FirstOrDefaultAsync(x => x.Name.ToLower() == key, cancellationToken);
            if (match is not null)
            {
                return match.Id;
            }
        }

        if (allowDefault)
        {
            var fallback = await _db.DocumentTypes.AsNoTracking()
                .FirstOrDefaultAsync(x => x.Id == SeedIds.TypeOther, cancellationToken)
                ?? await _db.DocumentTypes.AsNoTracking()
                    .OrderBy(x => x.SortOrder)
                    .FirstOrDefaultAsync(cancellationToken);
            if (fallback is not null)
            {
                return fallback.Id;
            }
        }

        throw new ValidationException("Document type is not valid.");
    }

    private void EnsureWithinSizeLimit(UploadWorkItemRequest request)
    {
        var size = request.FileSizeBytes > 0
            ? request.FileSizeBytes
            : request.Content.CanSeek ? request.Content.Length : 0;
        var max = _uploads.EffectiveMaxFileBytes;
        if (size > max)
        {
            throw new ValidationException(UploadOptions.OversizedMessage(request.FileName, size, max));
        }
    }

}
