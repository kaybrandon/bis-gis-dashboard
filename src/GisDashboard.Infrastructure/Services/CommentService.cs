using GisDashboard.Application.Abstractions;
using GisDashboard.Application.Exceptions;
using GisDashboard.Application.Notifications;
using GisDashboard.Application.WorkItems;
using GisDashboard.Domain;
using GisDashboard.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace GisDashboard.Infrastructure.Services;

public sealed class CommentService : ICommentService
{
    private readonly AppDbContext _db;
    private readonly ICurrentUser _currentUser;
    private readonly IOrgScope _orgScope;
    private readonly IWorkflowComms _comms;
    private readonly INotificationService _notifications;

    public CommentService(
        AppDbContext db,
        ICurrentUser currentUser,
        IOrgScope orgScope,
        IWorkflowComms comms,
        INotificationService notifications)
    {
        _db = db;
        _currentUser = currentUser;
        _orgScope = orgScope;
        _comms = comms;
        _notifications = notifications;
    }

    public async Task<IReadOnlyList<CommentDto>> ListAsync(Guid workItemId, CancellationToken cancellationToken = default)
    {
        await EnsureWorkItemInScopeAsync(workItemId, cancellationToken);
        var rows = await _db.WorkItemComments.AsNoTracking()
            .Where(x => x.WorkItemId == workItemId)
            .OrderBy(x => x.CreatedAtSort)
            .Include(x => x.AuthorUser)
            .Select(x => new
            {
                x.Id,
                x.WorkItemId,
                x.Body,
                x.AuthorUserId,
                Name = x.AuthorUser.FullName != null && x.AuthorUser.FullName != ""
                    ? x.AuthorUser.FullName
                    : x.AuthorUser.DisplayName,
                x.AuthorUser.IsArchived,
                x.CreatedAt
            })
            .ToListAsync(cancellationToken);
        return rows
            .Select(x => new CommentDto(
                x.Id,
                x.WorkItemId,
                x.Body,
                x.AuthorUserId,
                UserIdentity.WithArchivedSuffix(x.Name, x.IsArchived),
                x.CreatedAt))
            .ToList();
    }

    public async Task<CommentDto> CreateAsync(Guid workItemId, CreateCommentRequest request, CancellationToken cancellationToken = default)
    {
        await EnsureWorkItemInScopeAsync(workItemId, cancellationToken);
        if (!_currentUser.CanPostComments)
        {
            throw new ForbiddenException("Your role cannot post comments.");
        }

        var body = (request.Body ?? string.Empty).Trim();
        if (string.IsNullOrWhiteSpace(body))
        {
            throw new ValidationException("Comment text is required.");
        }

        if (body.Length > 2000)
        {
            body = body[..2000];
        }

        var now = DateTimeOffset.UtcNow;
        var comment = new WorkItemComment
        {
            Id = Guid.NewGuid(),
            WorkItemId = workItemId,
            AuthorUserId = _currentUser.UserId,
            Body = body,
            CreatedAt = now,
            CreatedAtSort = now.ToUnixTimeMilliseconds()
        };
        _db.WorkItemComments.Add(comment);
        await _db.SaveChangesAsync(cancellationToken);

        var item = await _db.WorkItems
            .Include(x => x.Organization)
            .FirstAsync(x => x.Id == workItemId, cancellationToken);
        var authorName = string.IsNullOrWhiteSpace(_currentUser.DisplayName)
            ? "Someone"
            : _currentUser.DisplayName;
        try
        {
            await _comms.NotifyClientCommentAsync(item, authorName, body, _currentUser.UserId, cancellationToken);
        }
        catch (Exception)
        {
            // Fail closed: the comment is already saved.
        }

        try
        {
            await _notifications.NotifyMentionsAsync(item, body, _currentUser.UserId, cancellationToken);
        }
        catch (Exception)
        {
            // Mentions are best-effort.
        }

        return new CommentDto(comment.Id, comment.WorkItemId, comment.Body, comment.AuthorUserId, authorName, comment.CreatedAt);
    }

    private async Task EnsureWorkItemInScopeAsync(Guid workItemId, CancellationToken cancellationToken)
    {
        var exists = await _db.WorkItems.AnyAsync(x => x.Id == workItemId, cancellationToken);
        if (!exists)
        {
            throw new NotFoundException("Document was not found.");
        }

        var allowed = await _orgScope.GetAllowedOrganizationIdsAsync(cancellationToken);
        var inScope = await _db.WorkItems.AnyAsync(x => x.Id == workItemId && allowed.Contains(x.OrganizationId), cancellationToken);
        if (!inScope)
        {
            throw new NotFoundException("Document was not found.");
        }
    }
}
