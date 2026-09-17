using System.Text.RegularExpressions;
using GisDashboard.Application.Abstractions;
using GisDashboard.Application.Exceptions;
using GisDashboard.Application.Presence;
using GisDashboard.Domain;
using GisDashboard.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace GisDashboard.Infrastructure.Services;

public sealed class PresenceService : IPresenceService
{
    public static readonly TimeSpan OnlineWindow = TimeSpan.FromSeconds(90);
    public static readonly TimeSpan OfflineAfter = TimeSpan.FromMinutes(3);

    private static readonly Regex DocumentRoute = new(
        @"^/documents/([0-9a-fA-F]{8}-[0-9a-fA-F]{4}-[0-9a-fA-F]{4}-[0-9a-fA-F]{4}-[0-9a-fA-F]{12})$",
        RegexOptions.Compiled | RegexOptions.CultureInvariant);

    private readonly AppDbContext _db;
    private readonly ICurrentUser _currentUser;
    private readonly IFileStorage _storage;

    public PresenceService(AppDbContext db, ICurrentUser currentUser, IFileStorage storage)
    {
        _db = db;
        _currentUser = currentUser;
        _storage = storage;
    }

    public async Task HeartbeatAsync(PresenceHeartbeatRequest request, CancellationToken cancellationToken = default)
    {
        if (!_currentUser.IsAuthenticated)
        {
            throw new ForbiddenException("Authentication is required.");
        }

        var now = DateTimeOffset.UtcNow;
        var route = NormalizeRoute(request.Route);
        var workItemId = await ResolveWorkItemIdAsync(request.WorkItemId, route, cancellationToken);
        Guid? clockWorkItemId = request.ClockWorkItemId is { } clockId && clockId != Guid.Empty
            ? clockId
            : null;

        var row = await _db.UserPresences.FirstOrDefaultAsync(x => x.UserId == _currentUser.UserId, cancellationToken);
        if (row is null)
        {
            row = new UserPresence { UserId = _currentUser.UserId };
            _db.UserPresences.Add(row);
        }

        row.LastSeen = now;
        row.LastSeenSort = now.ToUnixTimeMilliseconds();
        row.Route = route;
        row.WorkItemId = workItemId;
        row.ClockedIn = request.ClockedIn;
        row.ClockWorkItemId = request.ClockedIn ? clockWorkItemId : null;
        await _db.SaveChangesAsync(cancellationToken);
    }

    public async Task<PresenceListResponse> ListAsync(CancellationToken cancellationToken = default)
    {
        if (!_currentUser.IsAuthenticated)
        {
            throw new ForbiddenException("Authentication is required.");
        }

        if (!_currentUser.CanSeePresence)
        {
            throw new ForbiddenException("Presence is available to staff only.");
        }

        var cutoffSort = (DateTimeOffset.UtcNow - OfflineAfter).ToUnixTimeMilliseconds();
        var rows = await _db.UserPresences.AsNoTracking()
            .Where(x => x.LastSeenSort >= cutoffSort)
            .ToListAsync(cancellationToken);

        var visibleIds = await VisibleUserIdsAsync(rows.Select(x => x.UserId).ToList(), cancellationToken);
        rows = rows.Where(x => visibleIds.Contains(x.UserId)).ToList();
        if (rows.Count == 0)
        {
            return new PresenceListResponse([], 0);
        }

        var userIds = rows.Select(x => x.UserId).ToList();
        var users = await _db.Users.AsNoTracking()
            .Where(x => userIds.Contains(x.Id))
            .ToDictionaryAsync(x => x.Id, cancellationToken);

        var workItemIds = rows
            .Select(DisplayWorkItemId)
            .OfType<Guid>()
            .Distinct()
            .ToList();
        var titles = workItemIds.Count == 0
            ? new Dictionary<Guid, string>()
            : await _db.WorkItems.AsNoTracking()
                .Where(x => workItemIds.Contains(x.Id))
                .Select(x => new { x.Id, x.Title, x.FileName })
                .ToDictionaryAsync(
                    x => x.Id,
                    x => string.IsNullOrWhiteSpace(x.Title) ? x.FileName : x.Title,
                    cancellationToken);

        var now = DateTimeOffset.UtcNow;
        var items = rows
            .Select(row =>
            {
                users.TryGetValue(row.UserId, out var user);
                var workItemId = DisplayWorkItemId(row);
                string? title = null;
                if (workItemId is { } id)
                {
                    titles.TryGetValue(id, out title);
                }

                var age = now - row.LastSeen;
                var status = age <= OnlineWindow ? "Online" : "Away";
                var pageName = PageName(row.Route);
                return new PresenceUserDto(
                    row.UserId,
                    user is null
                        ? "Someone"
                        : UserIdentity.PublicName(user.FullName, user.DisplayName, user.UserName, user.Email),
                    user is not null && !string.IsNullOrWhiteSpace(user.AvatarBlobPath),
                    status,
                    pageName,
                    workItemId,
                    title,
                    row.ClockedIn,
                    row.LastSeen);
            })
            .OrderBy(x => x.PresenceStatus == "Online" ? 0 : 1)
            .ThenBy(x => x.DisplayName, StringComparer.OrdinalIgnoreCase)
            .ToList();

        return new PresenceListResponse(items, items.Count(x => x.PresenceStatus == "Online"));
    }

    public async Task<(Stream Stream, string ContentType)> GetAvatarAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        if (!_currentUser.IsAuthenticated)
        {
            throw new ForbiddenException("Authentication is required.");
        }

        if (!await CanSeeUserAsync(userId, cancellationToken))
        {
            throw new NotFoundException("Photo was not found.");
        }

        var user = await _db.Users.AsNoTracking().FirstOrDefaultAsync(x => x.Id == userId, cancellationToken);
        if (user is null || string.IsNullOrWhiteSpace(user.AvatarBlobPath))
        {
            throw new NotFoundException("Photo was not found.");
        }

        var stream = await _storage.OpenReadAsync(user.AvatarBlobPath, cancellationToken);
        return (stream, string.IsNullOrWhiteSpace(user.AvatarContentType) ? "image/jpeg" : user.AvatarContentType);
    }

    private static Guid? DisplayWorkItemId(UserPresence row) =>
        row.WorkItemId ?? (row.ClockedIn ? row.ClockWorkItemId : null);

    private async Task<Guid?> ResolveWorkItemIdAsync(Guid? requested, string route, CancellationToken cancellationToken)
    {
        var candidate = requested is { } id && id != Guid.Empty ? id : ParseDocumentId(route);
        if (candidate is null)
        {
            return null;
        }

        var exists = await _db.WorkItems.AsNoTracking().AnyAsync(x => x.Id == candidate.Value, cancellationToken);
        if (exists)
        {
            return candidate;
        }

        var fromRoute = ParseDocumentId(route);
        if (fromRoute is null || fromRoute == candidate)
        {
            return null;
        }

        return await _db.WorkItems.AsNoTracking().AnyAsync(x => x.Id == fromRoute.Value, cancellationToken)
            ? fromRoute
            : null;
    }

    private async Task<HashSet<Guid>> VisibleUserIdsAsync(IReadOnlyCollection<Guid> userIds, CancellationToken cancellationToken)
    {
        if (_currentUser.IsGlobalAdmin)
        {
            return userIds.ToHashSet();
        }

        var allowed = new HashSet<Guid> { _currentUser.UserId };
        foreach (var id in await GlobalAdminUserIdsAsync(cancellationToken))
        {
            allowed.Add(id);
        }

        var myOrgIds = await _db.UserOrganizations.AsNoTracking()
            .Where(x => x.UserId == _currentUser.UserId)
            .Select(x => x.OrganizationId)
            .ToListAsync(cancellationToken);
        if (myOrgIds.Count > 0)
        {
            var sameOrg = await _db.UserOrganizations.AsNoTracking()
                .Where(x => myOrgIds.Contains(x.OrganizationId))
                .Select(x => x.UserId)
                .ToListAsync(cancellationToken);
            foreach (var id in sameOrg)
            {
                allowed.Add(id);
            }
        }

        return userIds.Where(allowed.Contains).ToHashSet();
    }

    private async Task<bool> CanSeeUserAsync(Guid userId, CancellationToken cancellationToken)
    {
        if (userId == _currentUser.UserId)
        {
            return true;
        }

        if (!_currentUser.CanSeePresence)
        {
            return false;
        }

        if (_currentUser.IsGlobalAdmin)
        {
            return true;
        }

        if (await IsGlobalAdminUserAsync(userId, cancellationToken))
        {
            return true;
        }

        return await _db.UserOrganizations.AsNoTracking()
            .AnyAsync(
                mine => mine.UserId == _currentUser.UserId
                    && _db.UserOrganizations.Any(theirs => theirs.UserId == userId && theirs.OrganizationId == mine.OrganizationId),
                cancellationToken);
    }

    private async Task<List<Guid>> GlobalAdminUserIdsAsync(CancellationToken cancellationToken)
    {
        var roleId = await _db.Roles.AsNoTracking()
            .Where(x => x.Name == Roles.GlobalAdministrator)
            .Select(x => x.Id)
            .FirstOrDefaultAsync(cancellationToken);
        if (roleId == Guid.Empty)
        {
            return [];
        }

        return await _db.UserRoles.AsNoTracking()
            .Where(x => x.RoleId == roleId)
            .Select(x => x.UserId)
            .ToListAsync(cancellationToken);
    }

    private async Task<bool> IsGlobalAdminUserAsync(Guid userId, CancellationToken cancellationToken)
    {
        var roleId = await _db.Roles.AsNoTracking()
            .Where(x => x.Name == Roles.GlobalAdministrator)
            .Select(x => x.Id)
            .FirstOrDefaultAsync(cancellationToken);
        if (roleId == Guid.Empty)
        {
            return false;
        }

        return await _db.UserRoles.AsNoTracking()
            .AnyAsync(x => x.UserId == userId && x.RoleId == roleId, cancellationToken);
    }

    internal static string NormalizeRoute(string? route)
    {
        if (string.IsNullOrWhiteSpace(route))
        {
            return "/";
        }

        var value = route.Trim();
        var cut = value.IndexOfAny(['?', '#']);
        if (cut >= 0)
        {
            value = value[..cut];
        }

        if (value.StartsWith("http://", StringComparison.OrdinalIgnoreCase)
            || value.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
        {
            if (Uri.TryCreate(value, UriKind.Absolute, out var uri))
            {
                value = uri.AbsolutePath;
            }
        }

        if (!value.StartsWith('/'))
        {
            value = "/" + value;
        }

        if (value.Length > 200)
        {
            value = value[..200];
        }

        return string.IsNullOrWhiteSpace(value) ? "/" : value;
    }

    internal static Guid? ParseDocumentId(string route)
    {
        var match = DocumentRoute.Match(route);
        return match.Success && Guid.TryParse(match.Groups[1].Value, out var id) ? id : null;
    }

    internal static string PageName(string? route)
    {
        var path = NormalizeRoute(route);
        if (path == "/")
        {
            return "Dashboard";
        }

        if (path == "/documents")
        {
            return "Manage Documents";
        }

        if (DocumentRoute.IsMatch(path))
        {
            return "Work item";
        }

        if (path == "/upload-documents")
        {
            return "Upload Documents";
        }

        if (path == "/reports")
        {
            return "Reports";
        }

        if (path.StartsWith("/reports/", StringComparison.OrdinalIgnoreCase))
        {
            return "Report";
        }

        if (path == "/time-report")
        {
            return "Time Report";
        }

        if (path == "/profile")
        {
            return "Profile";
        }

        if (path == "/settings")
        {
            return "Settings";
        }

        if (path == "/status")
        {
            return "Status";
        }

        if (path == "/admin/users")
        {
            return "Users";
        }

        if (path == "/admin/organizations")
        {
            return "Organizations";
        }

        return "GIS Dashboard";
    }
}
