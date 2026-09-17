using GisDashboard.Domain;

namespace GisDashboard.Application.WorkItems;

public interface IWorkflowComms
{
    Task NotifyStatusChangedAsync(
        WorkItem item,
        string oldStatusName,
        string newStatusName,
        CancellationToken cancellationToken = default);

    Task NotifyClientCommentAsync(
        WorkItem item,
        string authorName,
        string body,
        Guid authorUserId,
        CancellationToken cancellationToken = default);

    Task NotifyPublicUploadReceivedAsync(
        Organization organization,
        int fileCount,
        IReadOnlyList<string> fileNames,
        CancellationToken cancellationToken = default);
}
