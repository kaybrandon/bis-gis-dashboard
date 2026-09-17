namespace GisDashboard.Application.Reports;

public interface IReportService
{
    Task<IReadOnlyList<ReportListItem>> ListAsync(Guid? organizationId, CancellationToken cancellationToken = default);
    Task<ReportDetail> GetAsync(Guid reportId, CancellationToken cancellationToken = default);
    Task<ReportDetail> GenerateAsync(GenerateReportRequest request, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<ReportRecipient>> ListRecipientsAsync(Guid organizationId, CancellationToken cancellationToken = default);
    Task<EmailReportResult> EmailAsync(Guid reportId, EmailReportRequest request, CancellationToken cancellationToken = default);
    Task<ReportFile> ExportPdfAsync(Guid reportId, CancellationToken cancellationToken = default);
    Task<ReportFile> ExportCompletedCsvAsync(Guid reportId, CancellationToken cancellationToken = default);
}
