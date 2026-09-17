namespace GisDashboard.Application.Notifications;

public sealed record NotificationDto(
    Guid Id,
    string Kind,
    string Title,
    string Body,
    Guid OrganizationId,
    Guid? WorkItemId,
    DateTimeOffset CreatedAt,
    bool Unread);

public sealed record NotificationListResponse(
    IReadOnlyList<NotificationDto> Items,
    int UnreadCount);
