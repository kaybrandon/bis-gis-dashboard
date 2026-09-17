using GisDashboard.Application.TimeReports;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace GisDashboard.Api.Controllers;

[ApiController]
[Authorize]
[Route("api/time-report")]
public sealed class TimeReportsController : ControllerBase
{
    private readonly ITimeReportService _reports;

    public TimeReportsController(ITimeReportService reports)
    {
        _reports = reports;
    }

    [HttpGet]
    public Task<TimeReportResponse> Get([FromQuery] TimeReportQuery query, CancellationToken cancellationToken) =>
        _reports.GetAsync(query, cancellationToken);

    [HttpGet("export")]
    public async Task<IActionResult> Export([FromQuery] TimeReportQuery query, CancellationToken cancellationToken)
    {
        var file = await _reports.ExportCsvAsync(query, cancellationToken);
        return File(file.Content, file.ContentType, file.FileName);
    }
}
