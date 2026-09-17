namespace GisDashboard.Application.TimeReports;

public interface ITimeReportService
{
    Task<TimeReportResponse> GetAsync(TimeReportQuery query, CancellationToken cancellationToken = default);
    Task<TimeReportFile> ExportCsvAsync(TimeReportQuery query, CancellationToken cancellationToken = default);
}
