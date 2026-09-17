using GisDashboard.Application.Connections;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace GisDashboard.Api.Controllers;

[ApiController]
[Authorize]
[Route("api/sync-control")]
public sealed class SyncControlController : ControllerBase
{
    private readonly IConnectionService _connections;

    public SyncControlController(IConnectionService connections)
    {
        _connections = connections;
    }

    [HttpGet]
    public Task<SyncControlDto> Get(CancellationToken cancellationToken) =>
        _connections.GetSyncControlAsync(cancellationToken);

    [HttpPost("pause")]
    public Task<SyncControlDto> Pause(CancellationToken cancellationToken) =>
        _connections.SetPausedAsync(true, cancellationToken);

    [HttpPost("resume")]
    public Task<SyncControlDto> Resume(CancellationToken cancellationToken) =>
        _connections.SetPausedAsync(false, cancellationToken);
}
