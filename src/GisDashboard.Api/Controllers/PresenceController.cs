using GisDashboard.Application.Presence;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace GisDashboard.Api.Controllers;

[ApiController]
[Authorize]
[Route("api/presence")]
public sealed class PresenceController : ControllerBase
{
    private readonly IPresenceService _presence;

    public PresenceController(IPresenceService presence)
    {
        _presence = presence;
    }

    [HttpPost]
    public async Task<IActionResult> Heartbeat([FromBody] PresenceHeartbeatRequest request, CancellationToken cancellationToken)
    {
        await _presence.HeartbeatAsync(request, cancellationToken);
        return NoContent();
    }

    [HttpGet]
    public Task<PresenceListResponse> List(CancellationToken cancellationToken) =>
        _presence.ListAsync(cancellationToken);

    [HttpPost("need-help")]
    public Task<NeedHelpResponse> NeedHelp([FromBody] NeedHelpRequest request, CancellationToken cancellationToken) =>
        _presence.SetNeedsHelpAsync(request.NeedsHelp, cancellationToken);

    [HttpGet("{userId:guid}/avatar")]
    public async Task<IActionResult> Avatar(Guid userId, CancellationToken cancellationToken)
    {
        var (stream, contentType) = await _presence.GetAvatarAsync(userId, cancellationToken);
        return File(stream, contentType);
    }
}
