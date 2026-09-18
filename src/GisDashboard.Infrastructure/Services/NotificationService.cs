using GisDashboard.Application.Abstractions;
using GisDashboard.Application.Exceptions;
using GisDashboard.Application.Notifications;
using GisDashboard.Domain;
using GisDashboard.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace GisDashboard.Infrastructure.Services;

public sealed class NotificationService : INotificationService
{
    private readonly AppDbContext _db;
    private readonly ICurrentUser _currentUser;

    public NotificationService(AppDbContext db, ICurrentUser currentUser)
    {
        _db = db;
        _currentUser = currentUser;
    }

    public async Task NotifyPriorityAsync(WorkItem item, Guid? actorUserId, CancellationToken cancellationToken = default)
    {
        var recipients = await ResolvePriorityRecipientsAsync(item, actorUserId, cancellationToken);
        if (recipients.Count == 0)
        {
            return;
        }

        var now = DateTimeOffset.UtcNow;
        var orgName = item.Organization?.Name;
        if (string.IsNullOrWhiteSpace(orgName))
        {
            orgName = await _db.Organizations.AsNoTracking()
                .Where(x => x.Id == item.OrganizationId)
                .Select(x => x.Name)
                .FirstOrDefaultAsync(cancellationToken);
        }

        var note = string.IsNullOrWhiteSpace(item.PriorityNote) ? "" : $" {item.PriorityNote.Trim()}";
        var needed = item.PriorityNeededBy is { } by
            ? $" Needed by {by:yyyy-MM-dd}."
            : "";
        var prefix = string.IsNullOrWhiteSpace(orgName) ? "" : $"{orgName}: ";
        var title = string.IsNullOrWhiteSpace(item.Title) ? item.FileName : item.Title;
        var body = $"{prefix}{item.FileName} was marked priority.{needed}{note}".Trim();
        var existing = await _db.Notifications
            .Where(x => x.WorkItemId == item.Id && x.Kind == "priority" && recipients.Contains(x.UserId))
            .ToListAsync(cancellationToken);

        foreach (var userId in recipients)
        {
            var row = existing.FirstOrDefault(x => x.UserId == userId);
            if (row is null)
            {
                _db.Notifications.Add(new InAppNotification
                {
                    Id = Guid.NewGuid(),
                    UserId = userId,
                    OrganizationId = item.OrganizationId,
                    WorkItemId = item.Id,
                    Kind = "priority",
                    Title = title,
                    Body = body,
                    CreatedAt = now,
                    CreatedAtSort = now.ToUnixTimeMilliseconds()
                });
            }
            else
            {
                row.Title = title;
                row.Body = body;
                row.CreatedAt = now;
                row.CreatedAtSort = now.ToUnixTimeMilliseconds();
                row.ReadAt = null;
            }
        }

        await _db.SaveChangesAsync(cancellationToken);
    }

    public async Task NotifyMentionsAsync(WorkItem item, string body, Guid authorUserId, CancellationToken cancellationToken = default)
    {
        var tokens = MentionTokens(body);
        if (tokens.Count == 0)
        {
            return;
        }

        var users = await _db.Users.AsNoTracking()
            .Where(x => x.IsActive && !x.IsArchived && x.Id != SeedIds.TokenUploadUser)
            .Select(x => new { x.Id, x.UserName, x.DisplayName, x.FullName })
            .ToListAsync(cancellationToken);

        var mentioned = new HashSet<Guid>();
        foreach (var token in tokens)
        {
            foreach (var user in users)
            {
                if (user.Id == authorUserId || mentioned.Contains(user.Id))
                {
                    continue;
                }

                if (MatchesMention(token, user.UserName, user.DisplayName, user.FullName))
                {
                    mentioned.Add(user.Id);
                }
            }
        }

        if (mentioned.Count == 0)
        {
            return;
        }

        var now = DateTimeOffset.UtcNow;
        var title = string.IsNullOrWhiteSpace(item.Title) ? item.FileName : item.Title;
        var excerpt = body.Length > 180 ? body[..180] + "…" : body;
        foreach (var userId in mentioned)
        {
            _db.Notifications.Add(new InAppNotification
            {
                Id = Guid.NewGuid(),
                UserId = userId,
                OrganizationId = item.OrganizationId,
                WorkItemId = item.Id,
                Kind = "mention",
                Title = title,
                Body = excerpt,
                CreatedAt = now,
                CreatedAtSort = now.ToUnixTimeMilliseconds()
            });
        }

        await _db.SaveChangesAsync(cancellationToken);
    }

    private static List<string> MentionTokens(string body)
    {
        var tokens = new List<string>();
        if (string.IsNullOrWhiteSpace(body))
        {
            return tokens;
        }

        foreach (System.Text.RegularExpressions.Match raw in System.Text.RegularExpressions.Regex.Matches(body, @"@([A-Za-z0-9._-]+)"))
        {
            var value = raw.Groups[1].Value.Trim();
            if (value.Length > 0)
            {
                tokens.Add(value);
            }
        }

        return tokens;
    }

    private static bool MatchesMention(string token, string? userName, string? displayName, string? fullName)
    {
        if (EqualsToken(token, userName) || EqualsToken(token, displayName) || EqualsToken(token, fullName))
        {
            return true;
        }

        if (!string.IsNullOrWhiteSpace(fullName))
        {
            var parts = fullName.Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
            if (parts.Any(part => EqualsToken(token, part)))
            {
                return true;
            }

            if (EqualsToken(token, string.Concat(parts)))
            {
                return true;
            }
        }

        return false;
    }

    private static bool EqualsToken(string token, string? value) =>
        !string.IsNullOrWhiteSpace(value) &&
        string.Equals(token, value.Trim(), StringComparison.OrdinalIgnoreCase);

    public async Task<NotificationListResponse> ListAsync(bool unreadOnly = false, CancellationToken cancellationToken = default)
    {
        if (!_currentUser.IsAuthenticated)
        {
            throw new ForbiddenException("Authentication is required.");
        }

        var query = _db.Notifications.AsNoTracking().Where(x => x.UserId == _currentUser.UserId);
        var unread = await query.CountAsync(x => x.ReadAt == null, cancellationToken);
        if (unreadOnly)
        {
            query = query.Where(x => x.ReadAt == null);
        }

        var items = await query
            .OrderByDescending(x => x.CreatedAtSort)
            .Take(40)
            .Select(x => new NotificationDto(x.Id, x.Kind, x.Title, x.Body, x.OrganizationId, x.WorkItemId, x.CreatedAt, x.ReadAt == null))
            .ToListAsync(cancellationToken);
        return new NotificationListResponse(items, unread);
    }

    public async Task MarkReadAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var item = await _db.Notifications.FirstOrDefaultAsync(
            x => x.Id == id && x.UserId == _currentUser.UserId,
            cancellationToken) ?? throw new NotFoundException("Notification was not found.");
        item.ReadAt ??= DateTimeOffset.UtcNow;
        await _db.SaveChangesAsync(cancellationToken);
    }

    public async Task MarkAllReadAsync(CancellationToken cancellationToken = default)
    {
        var unread = await _db.Notifications
            .Where(x => x.UserId == _currentUser.UserId && x.ReadAt == null)
            .ToListAsync(cancellationToken);
        var now = DateTimeOffset.UtcNow;
        foreach (var item in unread)
        {
            item.ReadAt = now;
        }

        await _db.SaveChangesAsync(cancellationToken);
    }

    /// <summary>
    /// Notify organization assigned tech(s), the work-item Assigned To when set,
    /// and every Global Administrator. A Global Admin who flagged the item still
    /// receives the alert; other self-actors do not.
    /// </summary>
    private async Task<List<Guid>> ResolvePriorityRecipientsAsync(WorkItem item, Guid? actorUserId, CancellationToken cancellationToken)
    {
        var ids = new HashSet<Guid>();
        var techs = await _db.OrganizationTechs.AsNoTracking()
            .Where(x => x.OrganizationId == item.OrganizationId)
            .Select(x => x.UserId)
            .ToListAsync(cancellationToken);
        foreach (var id in techs)
        {
            ids.Add(id);
        }

        if (item.AssignedToUserId is { } assignee)
        {
            ids.Add(assignee);
        }

        var globalRoleId = await _db.Roles.AsNoTracking()
            .Where(x => x.Name == Roles.GlobalAdministrator)
            .Select(x => x.Id)
            .FirstOrDefaultAsync(cancellationToken);
        var globals = globalRoleId == Guid.Empty
            ? []
            : await (
                from user in _db.Users.AsNoTracking()
                join ur in _db.UserRoles on user.Id equals ur.UserId
                where user.IsActive && !user.IsArchived && ur.RoleId == globalRoleId
                select user.Id
            ).ToListAsync(cancellationToken);
        foreach (var id in globals)
        {
            ids.Add(id);
        }

        if (actorUserId is { } actor && !globals.Contains(actor))
        {
            ids.Remove(actor);
        }

        ids.Remove(SeedIds.TokenUploadUser);
        return ids.ToList();
    }
}
