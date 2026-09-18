using GisDashboard.Domain;

namespace GisDashboard.Application.AiFill;

public interface IWorkItemAutoAiScanScheduler
{
    /// <summary>
    /// Stamps AI-scan fields on a newly uploaded work item. Returns true when a
    /// background scan should be enqueued after the upload is committed.
    /// Never throws.
    /// </summary>
    bool PrepareNewUpload(WorkItem item);

    void Enqueue(Guid workItemId);
}
