using GisDashboard.Application.Abstractions;
using GisDashboard.Application.Exceptions;
using GisDashboard.Application.WorkItems;
using GisDashboard.Domain;
using GisDashboard.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace GisDashboard.Infrastructure.Services;

public sealed class TimeEntryService : ITimeEntryService
{
    private readonly AppDbContext _db;
    private readonly ICurrentUser _currentUser;
    private readonly IOrgScope _orgScope;

    public TimeEntryService(AppDbContext db, ICurrentUser currentUser, IOrgScope orgScope)
    {
        _db = db;
        _currentUser = currentUser;
        _orgScope = orgScope;
    }

    public async Task<TimeEntryListResponse> ListAsync(Guid workItemId, CancellationToken cancellationToken = default)
    {
        await LoadWorkItemForTimeAsync(workItemId, requireSee: true, requireLog: false, cancellationToken);

        var entries = await _db.TimeEntries.AsNoTracking()
            .Where(x => x.WorkItemId == workItemId)
            .OrderByDescending(x => x.WorkedOnSort)
            .ThenByDescending(x => x.CreatedAtSort)
            .Include(x => x.LoggedByUser)
            .ToListAsync(cancellationToken);

        var items = entries.Select(Map).ToList();
        var total = items.Sum(x => x.Minutes);
        return new TimeEntryListResponse(items, total, TimeDurations.Format(total));
    }

    public async Task<TimeEntryDto> CreateAsync(Guid workItemId, UpsertTimeEntryRequest request, CancellationToken cancellationToken = default)
    {
        await LoadWorkItemForTimeAsync(workItemId, requireSee: true, requireLog: true, cancellationToken);

        var now = DateTimeOffset.UtcNow;
        var entry = new TimeEntry
        {
            Id = Guid.NewGuid(),
            WorkItemId = workItemId,
            LoggedByUserId = _currentUser.UserId,
            Minutes = TimeDurations.ResolveMinutes(request.Hours, request.Minutes, request.DecimalHours),
            Note = NormalizeNote(request.Note)
        };
        entry.TouchWorkedOn(request.WorkedOn ?? now);
        entry.TouchCreated(now);

        _db.TimeEntries.Add(entry);
        await _db.SaveChangesAsync(cancellationToken);
        return Map(await LoadEntryAsync(workItemId, entry.Id, cancellationToken));
    }

    public async Task<TimeEntryDto> UpdateAsync(Guid workItemId, Guid entryId, UpsertTimeEntryRequest request, CancellationToken cancellationToken = default)
    {
        await LoadWorkItemForTimeAsync(workItemId, requireSee: true, requireLog: true, cancellationToken);
        var entry = await LoadEntryAsync(workItemId, entryId, cancellationToken);
        EnsureCanManage(entry);

        entry.Minutes = TimeDurations.ResolveMinutes(request.Hours, request.Minutes, request.DecimalHours);
        entry.Note = NormalizeNote(request.Note);
        if (request.WorkedOn is { } workedOn)
        {
            entry.TouchWorkedOn(workedOn);
        }

        entry.UpdatedAt = DateTimeOffset.UtcNow;
        await _db.SaveChangesAsync(cancellationToken);
        return Map(await LoadEntryAsync(workItemId, entryId, cancellationToken));
    }

    public async Task DeleteAsync(Guid workItemId, Guid entryId, CancellationToken cancellationToken = default)
    {
        await LoadWorkItemForTimeAsync(workItemId, requireSee: true, requireLog: true, cancellationToken);
        var entry = await LoadEntryAsync(workItemId, entryId, cancellationToken);
        EnsureCanManage(entry);
        _db.TimeEntries.Remove(entry);
        await _db.SaveChangesAsync(cancellationToken);
    }

    private async Task LoadWorkItemForTimeAsync(Guid workItemId, bool requireSee, bool requireLog, CancellationToken cancellationToken)
    {
        var exists = await _db.WorkItems.AnyAsync(x => x.Id == workItemId, cancellationToken);
        if (!exists)
        {
            throw new NotFoundException("Document was not found.");
        }

        var allowed = await _orgScope.GetAllowedOrganizationIdsAsync(cancellationToken);
        var inScope = await _db.WorkItems.AnyAsync(
            x => x.Id == workItemId && allowed.Contains(x.OrganizationId),
            cancellationToken);
        if (!inScope)
        {
            throw new NotFoundException("Document was not found.");
        }

        if (requireSee && !_currentUser.CanSeeTimeLogs)
        {
            throw new ForbiddenException("Time logs are internal.");
        }

        if (requireLog && !_currentUser.CanLogTime)
        {
            throw new ForbiddenException("Your role cannot log time.");
        }
    }

    private async Task<TimeEntry> LoadEntryAsync(Guid workItemId, Guid entryId, CancellationToken cancellationToken)
    {
        var entry = await _db.TimeEntries
            .Include(x => x.LoggedByUser)
            .FirstOrDefaultAsync(x => x.Id == entryId && x.WorkItemId == workItemId, cancellationToken);

        if (entry is null)
        {
            throw new NotFoundException("Time entry was not found.");
        }

        return entry;
    }

    private void EnsureCanManage(TimeEntry entry)
    {
        if (_currentUser.IsAdmin)
        {
            return;
        }

        if (_currentUser.CanLogTime && entry.LoggedByUserId == _currentUser.UserId)
        {
            return;
        }

        throw new ForbiddenException("You can only edit your own time entries.");
    }

    private TimeEntryDto Map(TimeEntry entry) =>
        new(
            entry.Id,
            entry.WorkItemId,
            entry.Minutes,
            TimeDurations.ToHours(entry.Minutes),
            TimeDurations.Format(entry.Minutes),
            entry.WorkedOn,
            entry.Note,
            entry.LoggedByUserId,
            entry.LoggedByUser.PublicName,
            entry.CreatedAt,
            entry.UpdatedAt,
            _currentUser.IsAdmin || (_currentUser.CanLogTime && entry.LoggedByUserId == _currentUser.UserId));

    private static string? NormalizeNote(string? note)
    {
        if (string.IsNullOrWhiteSpace(note))
        {
            return null;
        }

        var trimmed = note.Trim();
        return trimmed.Length > 500 ? trimmed[..500] : trimmed;
    }
}
