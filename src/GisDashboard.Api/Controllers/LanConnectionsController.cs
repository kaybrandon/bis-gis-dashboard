using GisDashboard.Application.Connections;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace GisDashboard.Api.Controllers;

[ApiController]
[Authorize]
[Route("api/lan-connections")]
public sealed class LanConnectionsController : ControllerBase
{
    private readonly IConnectionService _connections;

    public LanConnectionsController(IConnectionService connections)
    {
        _connections = connections;
    }

    [HttpGet]
    public Task<IReadOnlyList<LanConnectionDto>> List(CancellationToken cancellationToken) =>
        _connections.ListLanConnectionsAsync(cancellationToken);

    [HttpGet("{id:guid}")]
    public Task<LanConnectionDto> Get(Guid id, [FromQuery] bool revealToken = false, CancellationToken cancellationToken = default) =>
        _connections.GetLanConnectionAsync(id, revealToken, cancellationToken);

    [HttpPost]
    public Task<LanConnectionDto> Create([FromBody] UpsertLanConnectionRequest request, CancellationToken cancellationToken) =>
        _connections.CreateLanConnectionAsync(request, cancellationToken);

    [HttpPatch("{id:guid}")]
    public Task<LanConnectionDto> Update(Guid id, [FromBody] UpsertLanConnectionRequest request, CancellationToken cancellationToken) =>
        _connections.UpdateLanConnectionAsync(id, request, cancellationToken);

    [HttpPost("{id:guid}/run-now")]
    public Task<LanConnectionDto> RunNow(Guid id, CancellationToken cancellationToken) =>
        _connections.RunLanNowAsync(id, cancellationToken);

    [HttpPost("{id:guid}/retry")]
    public Task<LanConnectionDto> Retry(Guid id, CancellationToken cancellationToken) =>
        _connections.RetryLanAsync(id, cancellationToken);

    [HttpPost("{id:guid}/rotate-token")]
    public Task<LanConnectionDto> RotateToken(Guid id, CancellationToken cancellationToken) =>
        _connections.RotateLanTokenAsync(id, cancellationToken);

    [HttpPost("{id:guid}/check-folders")]
    public Task<CheckFoldersResponse> CheckFolders(Guid id, CancellationToken cancellationToken) =>
        _connections.CheckLanFoldersAsync(id, cancellationToken);

    [HttpPost("{id:guid}/restart")]
    public Task<LanConnectionDto> Restart(Guid id, CancellationToken cancellationToken) =>
        _connections.GetLanConnectionAsync(id, cancellationToken: cancellationToken);

    [HttpPost("{id:guid}/update")]
    public Task<LanConnectionDto> UpdateAgent(Guid id, CancellationToken cancellationToken) =>
        _connections.GetLanConnectionAsync(id, cancellationToken: cancellationToken);

    [HttpPost("update-all")]
    public async Task<IActionResult> UpdateAll(CancellationToken cancellationToken)
    {
        var items = await _connections.ListLanConnectionsAsync(cancellationToken);
        return Ok(new { items, queued = 0 });
    }

    [HttpGet("map")]
    public IActionResult Map() => Ok(Array.Empty<object>());

    [AllowAnonymous]
    [HttpPost("agent/heartbeat")]
    public Task<LanConnectionDto> Heartbeat([FromBody] AgentHeartbeatRequest request, CancellationToken cancellationToken) =>
        _connections.AgentHeartbeatAsync(request, cancellationToken);

    [HttpGet("agent/windows-installer/info")]
    public IActionResult InstallerInfo() =>
        Ok(new { available = false, version = (string?)null });

    [HttpGet("agent/windows-installer")]
    public IActionResult Installer() =>
        StatusCode(StatusCodes.Status503ServiceUnavailable, new { message = "Installer is not packaged on this host yet." });

    [HttpGet("agent/windows")]
    public IActionResult AgentZip() =>
        StatusCode(StatusCodes.Status503ServiceUnavailable, new { message = "Could not download the fallback zip." });
}
