using GisDashboard.Application.Abstractions;
using GisDashboard.Application.Exceptions;
using GisDashboard.Application.TimeReports;
using GisDashboard.Application.WorkItems;
using GisDashboard.Domain;
using GisDashboard.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace GisDashboard.Infrastructure.Services;

public sealed class TimeReportService : ITimeReportService
{
    private readonly AppDbContext _db;
    private readonly ICurrentUser _currentUser;
    private readonly IOrgScope _orgScope;

    public TimeReportService(AppDbContext db, ICurrentUser currentUser, IOrgScope orgScope)
    {
        _db = db;
        _currentUser = currentUser;
        _orgScope = orgScope;
    }

    public async Task<TimeReportResponse> GetAsync(TimeReportQuery query, CancellationToken cancellationToken = default)
    {
        var (from, to, bucket, rows, people, canViewTeam) = await LoadAsync(query, cancellationToken);
        return Build(from, to, bucket, rows, people, canViewTeam);
    }

    public async Task<TimeReportFile> ExportCsvAsync(TimeReportQuery query, CancellationToken cancellationToken = default)
    {
        var report = await GetAsync(query, cancellationToken);
        return TimeReportCsv.Build(report);
    }

    private async Task<(DateOnly From, DateOnly To, string Bucket, List<Row> Rows, List<TimeReportPersonOption> People, bool CanViewTeam)> LoadAsync(
        TimeReportQuery query,
        CancellationToken cancellationToken)
    {
        if (!_currentUser.IsAuthenticated)
        {
            throw new ForbiddenException("Authentication is required.");
        }

        var allowed = (await _orgScope.GetAllowedOrganizationIdsAsync(cancellationToken)).ToHashSet();
        if (query.OrganizationId is { } orgId)
        {
            if (!allowed.Contains(orgId))
            {
                throw new NotFoundException("Organization was not found.");
            }

            allowed = [orgId];
        }

        var visibleOrgIds = await _db.Organizations.AsNoTracking()
            .Where(x => allowed.Contains(x.Id) && x.TimeReportCardsVisible)
            .Select(x => x.Id)
            .ToListAsync(cancellationToken);

        var role = _currentUser.Role;
        var canViewTeam = _currentUser.IsGlobalAdmin
            || (role is Roles.Administrator or Roles.Viewer && visibleOrgIds.Count > 0);
        var canViewOwn = _currentUser.IsGlobalAdmin || role is Roles.Editor or Roles.Administrator;
        if (!canViewOwn && !canViewTeam)
        {
            throw new ForbiddenException("Time report cards are not enabled for your organization.");
        }

        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        var from = query.From ?? new DateOnly(today.Year, today.Month, 1);
        var to = query.To ?? today;
        if (to < from)
        {
            throw new ValidationException("The end date cannot be before the start date.");
        }

        var bucket = NormalizeBucket(query.Bucket);
        var fromSort = new DateTimeOffset(from.ToDateTime(TimeOnly.MinValue), TimeSpan.Zero).ToUnixTimeMilliseconds();
        var toSort = new DateTimeOffset(to.AddDays(1).ToDateTime(TimeOnly.MinValue), TimeSpan.Zero).ToUnixTimeMilliseconds();
        var allowedIds = allowed.ToList();
        var currentUserId = _currentUser.UserId;

        if (query.UserId is { } requestedUser && !canViewTeam && requestedUser != currentUserId)
        {
            throw new ForbiddenException("You can only open your own time report card.");
        }

        var raw = await _db.TimeEntries.AsNoTracking()
            .Where(x => allowedIds.Contains(x.WorkItem.OrganizationId)
                && x.WorkedOnSort >= fromSort
                && x.WorkedOnSort < toSort)
            .Select(x => new Row(
                x.WorkedOn,
                x.LoggedByUserId,
                x.LoggedByUser.FullName != null && x.LoggedByUser.FullName != ""
                    ? x.LoggedByUser.FullName
                    : x.LoggedByUser.DisplayName,
                x.WorkItem.OrganizationId,
                x.WorkItem.Organization.Name,
                x.WorkItemId,
                x.WorkItem.FileName,
                x.Minutes,
                x.Note,
                x.WorkedOnSort))
            .ToListAsync(cancellationToken);

        IEnumerable<Row> scoped = raw;
        if (_currentUser.IsGlobalAdmin)
        {
            // Team hours across the selected organizations.
        }
        else if (role == Roles.Editor)
        {
            scoped = scoped.Where(x => x.LoggedByUserId == currentUserId);
        }
        else if (role == Roles.Administrator)
        {
            scoped = scoped.Where(x =>
                x.LoggedByUserId == currentUserId
                || visibleOrgIds.Contains(x.OrganizationId));
        }
        else
        {
            scoped = scoped.Where(x => visibleOrgIds.Contains(x.OrganizationId));
        }

        var beforeUserFilter = scoped.ToList();
        var people = beforeUserFilter
            .GroupBy(x => x.LoggedByUserId)
            .Select(g => new TimeReportPersonOption(g.Key, g.First().LoggedByName))
            .OrderBy(x => x.Name)
            .ToList();

        if (query.UserId is { } userId)
        {
            beforeUserFilter = beforeUserFilter.Where(x => x.LoggedByUserId == userId).ToList();
        }

        var rows = beforeUserFilter
            .OrderByDescending(x => x.WorkedOnSort)
            .ThenBy(x => x.LoggedByName)
            .ToList();

        return (from, to, bucket, rows, people, canViewTeam);
    }

    private static TimeReportResponse Build(
        DateOnly from,
        DateOnly to,
        string bucket,
        List<Row> rows,
        List<TimeReportPersonOption> people,
        bool canViewTeam)
    {
        var total = rows.Sum(x => x.Minutes);
        var byPerson = rows
            .GroupBy(x => new { x.LoggedByUserId, x.LoggedByName })
            .Select(g => Named(g.Key.LoggedByUserId, g.Key.LoggedByName, g.Sum(x => x.Minutes), g.Count()))
            .OrderByDescending(x => x.Minutes)
            .ThenBy(x => x.Name)
            .ToList();
        var byClient = rows
            .GroupBy(x => new { x.OrganizationId, x.OrganizationName })
            .Select(g => Named(g.Key.OrganizationId, g.Key.OrganizationName, g.Sum(x => x.Minutes), g.Count()))
            .OrderByDescending(x => x.Minutes)
            .ThenBy(x => x.Name)
            .ToList();
        var byPeriod = rows
            .GroupBy(x => Period(x.WorkedOn, bucket))
            .Select(g => new TimeReportPeriodTotal(
                g.Key.Key,
                g.Key.Label,
                g.Sum(x => x.Minutes),
                TimeDurations.ToHours(g.Sum(x => x.Minutes)),
                TimeDurations.Format(g.Sum(x => x.Minutes)),
                g.Count()))
            .OrderBy(x => x.Key)
            .ToList();
        var byWorkItem = rows
            .GroupBy(x => new { x.WorkItemId, x.FileName, x.OrganizationName })
            .Select(g => new TimeReportWorkItemTotal(
                g.Key.WorkItemId,
                g.Key.FileName,
                g.Key.OrganizationName,
                g.Sum(x => x.Minutes),
                TimeDurations.ToHours(g.Sum(x => x.Minutes)),
                TimeDurations.Format(g.Sum(x => x.Minutes)),
                g.Count()))
            .OrderByDescending(x => x.Minutes)
            .ThenBy(x => x.FileName)
            .ToList();
        var entries = rows.Select(x => new TimeReportLine(
            x.WorkedOn,
            x.LoggedByName,
            x.OrganizationName,
            x.FileName,
            x.WorkItemId,
            x.Minutes,
            TimeDurations.ToHours(x.Minutes),
            TimeDurations.Format(x.Minutes),
            x.Note)).ToList();

        return new TimeReportResponse(
            from,
            to,
            bucket,
            canViewTeam,
            total,
            TimeDurations.ToHours(total),
            TimeDurations.Format(total),
            byPerson.Count,
            byClient.Count,
            byWorkItem.Count,
            people,
            byPerson,
            byClient,
            byPeriod,
            byWorkItem,
            entries);
    }

    private static TimeReportNamedTotal Named(Guid id, string name, int minutes, int count) =>
        new(id, name, minutes, TimeDurations.ToHours(minutes), TimeDurations.Format(minutes), count);

    private static string NormalizeBucket(string? bucket)
    {
        var value = (bucket ?? "month").Trim().ToLowerInvariant();
        return value switch
        {
            "day" or "week" or "month" => value,
            _ => throw new ValidationException("Period must be day, week, or month.")
        };
    }

    private static (string Key, string Label) Period(DateTimeOffset workedOn, string bucket)
    {
        var date = DateOnly.FromDateTime(workedOn.Date);
        return bucket switch
        {
            "day" => (date.ToString("yyyy-MM-dd"), date.ToString("MMM d, yyyy")),
            "week" => Week(date),
            _ => (date.ToString("yyyy-MM"), date.ToString("MMMM yyyy"))
        };
    }

    private static (string Key, string Label) Week(DateOnly date)
    {
        var delta = ((int)date.DayOfWeek + 6) % 7;
        var start = date.AddDays(-delta);
        return (start.ToString("yyyy-MM-dd"), $"Week of {start:MMM d, yyyy}");
    }

    private sealed record Row(
        DateTimeOffset WorkedOn,
        Guid LoggedByUserId,
        string LoggedByName,
        Guid OrganizationId,
        string OrganizationName,
        Guid WorkItemId,
        string FileName,
        int Minutes,
        string? Note,
        long WorkedOnSort);
}
