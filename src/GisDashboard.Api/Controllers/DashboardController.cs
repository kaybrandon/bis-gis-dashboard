using GisDashboard.Application.WorkItems;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace GisDashboard.Api.Controllers;

[ApiController]
[Authorize]
[Route("api/dashboard")]
public sealed class DashboardController : ControllerBase
{
    private readonly IWorkItemService _workItems;
    private readonly IDashboardReportService _reports;

    public DashboardController(IWorkItemService workItems, IDashboardReportService reports)
    {
        _workItems = workItems;
        _reports = reports;
    }

    [HttpGet]
    public Task<DashboardResponse> Get([FromQuery] DashboardQuery query, CancellationToken cancellationToken) =>
        _workItems.GetDashboardAsync(query, cancellationToken);

    [HttpGet("pdf")]
    public async Task<IActionResult> Pdf([FromQuery] DashboardQuery query, CancellationToken cancellationToken)
    {
        var file = await _reports.ExportPdfAsync(query, cancellationToken);
        return File(file.Content, file.ContentType, file.FileName);
    }

    [HttpGet("recipients")]
    public Task<IReadOnlyList<DashboardRecipient>> Recipients(
        [FromQuery] Guid? organizationId,
        CancellationToken cancellationToken) =>
        _reports.ListRecipientsAsync(organizationId, cancellationToken);

    [HttpPost("email")]
    public Task<DashboardEmailResult> Email(
        [FromQuery] DashboardQuery query,
        [FromBody] DashboardEmailRequest request,
        CancellationToken cancellationToken) =>
        _reports.EmailAsync(query, request, cancellationToken);
}
