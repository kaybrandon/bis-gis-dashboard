using GisDashboard.Application.Status;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace GisDashboard.Api.Controllers;

[ApiController]
[Authorize]
[Route("api/admin/status")]
public sealed class StatusController : ControllerBase
{
    private readonly IEnvironmentStatusService _status;

    public StatusController(IEnvironmentStatusService status)
    {
        _status = status;
    }

    [HttpGet]
    public Task<EnvironmentStatusResponse> Get(CancellationToken cancellationToken) =>
        _status.GetAsync(cancellationToken);
}
