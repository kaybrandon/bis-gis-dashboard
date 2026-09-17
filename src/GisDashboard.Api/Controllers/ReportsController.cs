using GisDashboard.Application.Reports;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace GisDashboard.Api.Controllers;

[ApiController]
[Authorize]
[Route("api/reports")]
public sealed class ReportsController : ControllerBase
{
    private readonly IReportService _reports;

    public ReportsController(IReportService reports)
    {
        _reports = reports;
    }

    [HttpGet]
    public Task<IReadOnlyList<ReportListItem>> List([FromQuery] Guid? organizationId, CancellationToken cancellationToken) =>
        _reports.ListAsync(organizationId, cancellationToken);

    [HttpGet("recipients")]
    public Task<IReadOnlyList<ReportRecipient>> Recipients([FromQuery] Guid organizationId, CancellationToken cancellationToken) =>
        _reports.ListRecipientsAsync(organizationId, cancellationToken);

    [HttpGet("{id:guid}")]
    public Task<ReportDetail> Get(Guid id, CancellationToken cancellationToken) =>
        _reports.GetAsync(id, cancellationToken);

    [HttpPost("generate")]
    public async Task<ActionResult<ReportDetail>> Generate(
        [FromBody] GenerateReportRequest request,
        CancellationToken cancellationToken)
    {
        var created = await _reports.GenerateAsync(request, cancellationToken);
        return Created($"/api/reports/{created.Id}", created);
    }

    [HttpPost("{id:guid}/email")]
    public Task<EmailReportResult> Email(
        Guid id,
        [FromBody] EmailReportRequest request,
        CancellationToken cancellationToken) =>
        _reports.EmailAsync(id, request, cancellationToken);

    [HttpGet("{id:guid}/pdf")]
    public async Task<IActionResult> Pdf(Guid id, CancellationToken cancellationToken)
    {
        var file = await _reports.ExportPdfAsync(id, cancellationToken);
        return File(file.Content, file.ContentType, file.FileName);
    }

    [HttpGet("{id:guid}/csv")]
    public async Task<IActionResult> Csv(Guid id, CancellationToken cancellationToken)
    {
        var file = await _reports.ExportCompletedCsvAsync(id, cancellationToken);
        return File(file.Content, file.ContentType, file.FileName);
    }
}
