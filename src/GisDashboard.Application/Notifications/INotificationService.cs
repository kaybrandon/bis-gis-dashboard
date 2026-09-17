using GisDashboard.Domain;

namespace GisDashboard.Application.Notifications;

public interface INotificationService
{
    Task NotifyPriorityAsync(WorkItem item, Guid? actorUserId, CancellationToken cancellationToken = default);
    Task NotifyMentionsAsync(WorkItem item, string body, Guid authorUserId, CancellationToken cancellationToken = default);
    Task<NotificationListResponse> ListAsync(bool unreadOnly = false, CancellationToken cancellationToken = default);
    Task MarkReadAsync(Guid id, CancellationToken cancellationToken = default);
    Task MarkAllReadAsync(CancellationToken cancellationToken = default);
}
