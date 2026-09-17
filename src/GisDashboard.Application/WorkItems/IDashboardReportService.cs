namespace GisDashboard.Application.WorkItems;

public interface IDashboardReportService
{
    Task<ExcelExport> ExportPdfAsync(DashboardQuery query, CancellationToken cancellationToken = default);
    Task<DashboardEmailResult> EmailAsync(DashboardQuery query, DashboardEmailRequest request, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<DashboardRecipient>> ListRecipientsAsync(Guid? organizationId, CancellationToken cancellationToken = default);
}
