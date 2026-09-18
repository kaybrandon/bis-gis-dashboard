namespace GisDashboard.Application.WorkItems;

public interface IWorkItemService
{
    Task<WorkItemListResponse> ListAsync(WorkItemQuery query, CancellationToken cancellationToken = default);
    Task<WorkItemDetail> GetAsync(Guid id, CancellationToken cancellationToken = default);
    Task<WorkItemNeighbors> GetNeighborsAsync(Guid id, WorkItemQuery query, CancellationToken cancellationToken = default);
    Task<WorkItemDetail> UploadAsync(UploadWorkItemRequest request, CancellationToken cancellationToken = default);
    Task<WorkItemDetail> UpdateAsync(Guid id, UpdateWorkItemRequest request, CancellationToken cancellationToken = default);
    Task<FileDownload> OpenFileAsync(Guid id, CancellationToken cancellationToken = default);
    Task<FilePreview> OpenPreviewAsync(Guid id, CancellationToken cancellationToken = default);
    Task<DashboardResponse> GetDashboardAsync(DashboardQuery query, CancellationToken cancellationToken = default);
    Task<ExcelExport> ExportAsync(WorkItemQuery query, CancellationToken cancellationToken = default);
    Task<PublicUploadInfo> GetPublicUploadAsync(string token, CancellationToken cancellationToken = default);
    Task<PublicUploadResult> UploadByTokenAsync(string token, UploadWorkItemRequest request, CancellationToken cancellationToken = default);
    Task NotifyPublicUploadReceivedAsync(string token, PublicUploadReceivedRequest request, CancellationToken cancellationToken = default);
}
