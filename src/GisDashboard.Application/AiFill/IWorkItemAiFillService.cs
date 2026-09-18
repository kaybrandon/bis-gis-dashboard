namespace GisDashboard.Application.AiFill;

public interface IWorkItemAiFillService
{
    Task<AiFillResponse> FillFromPdfAsync(
        Guid workItemId,
        bool rescore = false,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Server-side auto scan after a successful PDF upload. Never throws to the
    /// caller for model/config failures — those become a failed/unconfigured scan state.
    /// </summary>
    Task RunAutoScanAsync(Guid workItemId, CancellationToken cancellationToken = default);
}
