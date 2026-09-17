using GisDashboard.Application.Notifications;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace GisDashboard.Api.Controllers;

[ApiController]
[Authorize]
[Route("api/notifications")]
public sealed class NotificationsController : ControllerBase
{
    private readonly INotificationService _notifications;

    public NotificationsController(INotificationService notifications)
    {
        _notifications = notifications;
    }

    [HttpGet]
    public Task<NotificationListResponse> List([FromQuery] bool unreadOnly, CancellationToken cancellationToken) =>
        _notifications.ListAsync(unreadOnly, cancellationToken);

    [HttpPost("{id:guid}/read")]
    public async Task<IActionResult> Read(Guid id, CancellationToken cancellationToken)
    {
        await _notifications.MarkReadAsync(id, cancellationToken);
        return NoContent();
    }

    [HttpPost("read-all")]
    public async Task<IActionResult> ReadAll(CancellationToken cancellationToken)
    {
        await _notifications.MarkAllReadAsync(cancellationToken);
        return NoContent();
    }
}
