using GisDashboard.Application.Connections;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace GisDashboard.Api.Controllers;

[ApiController]
[Authorize]
[Route("api/connections")]
public sealed class ConnectionsController : ControllerBase
{
    private readonly IConnectionService _connections;

    public ConnectionsController(IConnectionService connections)
    {
        _connections = connections;
    }

    [HttpGet]
    public Task<IReadOnlyList<FileConnectionDto>> List(CancellationToken cancellationToken) =>
        _connections.ListFileConnectionsAsync(cancellationToken);

    [HttpGet("{id:guid}")]
    public Task<FileConnectionDto> Get(Guid id, CancellationToken cancellationToken) =>
        _connections.GetFileConnectionAsync(id, cancellationToken);

    [HttpPost]
    public Task<FileConnectionDto> Create([FromBody] UpsertFileConnectionRequest request, CancellationToken cancellationToken) =>
        _connections.CreateFileConnectionAsync(request, cancellationToken);

    [HttpPatch("{id:guid}")]
    public Task<FileConnectionDto> Update(Guid id, [FromBody] UpsertFileConnectionRequest request, CancellationToken cancellationToken) =>
        _connections.UpdateFileConnectionAsync(id, request, cancellationToken);

    [HttpPost("{id:guid}/run-now")]
    public Task<FileConnectionDto> RunNow(Guid id, CancellationToken cancellationToken) =>
        _connections.RunFileConnectionNowAsync(id, cancellationToken);

    [HttpPost("check-folders")]
    public Task<CheckFoldersResponse> CheckFolders([FromBody] CheckFoldersRequest request, CancellationToken cancellationToken) =>
        _connections.CheckFoldersAsync(request, cancellationToken);

    [HttpGet("agent-package")]
    public IActionResult AgentPackage() =>
        StatusCode(StatusCodes.Status503ServiceUnavailable, new { message = "Installer is not packaged on this host yet." });
}
