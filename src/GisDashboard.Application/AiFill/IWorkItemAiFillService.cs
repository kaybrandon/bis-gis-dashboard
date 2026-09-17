namespace GisDashboard.Application.AiFill;

public interface IWorkItemAiFillService
{
    Task<AiFillResponse> FillFromPdfAsync(Guid workItemId, CancellationToken cancellationToken = default);
}
