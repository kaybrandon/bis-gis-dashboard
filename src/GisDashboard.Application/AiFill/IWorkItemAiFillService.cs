namespace GisDashboard.Application.AiFill;

public interface IWorkItemAiFillService
{
    Task<AiFillResponse> FillFromPdfAsync(
        Guid workItemId,
        bool rescore = false,
        CancellationToken cancellationToken = default);
}
