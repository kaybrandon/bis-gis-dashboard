using GisDashboard.Application.Connections;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace GisDashboard.Api.Controllers;

[ApiController]
[Authorize]
[Route("api/file-servers")]
public sealed class FileServersController : ControllerBase
{
    private readonly IConnectionService _connections;

    public FileServersController(IConnectionService connections)
    {
        _connections = connections;
    }

    [HttpGet]
    public Task<IReadOnlyList<FileServerDto>> List(CancellationToken cancellationToken) =>
        _connections.ListFileServersAsync(cancellationToken);

    [HttpPost]
    public Task<FileServerDto> Create([FromBody] CreateFileServerRequest request, CancellationToken cancellationToken) =>
        _connections.CreateFileServerAsync(request.Name ?? "File server", request.RootPath ?? "", cancellationToken);

    [HttpPut("{id:guid}")]
    public Task<IReadOnlyList<FileServerDto>> Update(Guid id, CancellationToken cancellationToken) =>
        _connections.ListFileServersAsync(cancellationToken);
}

public sealed record CreateFileServerRequest(string? Name, string? RootPath);
