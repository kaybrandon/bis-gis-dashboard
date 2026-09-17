namespace GisDashboard.Application.WorkItems;

public interface ITimeEntryService
{
    Task<TimeEntryListResponse> ListAsync(Guid workItemId, CancellationToken cancellationToken = default);
    Task<TimeEntryDto> CreateAsync(Guid workItemId, UpsertTimeEntryRequest request, CancellationToken cancellationToken = default);
    Task<TimeEntryDto> UpdateAsync(Guid workItemId, Guid entryId, UpsertTimeEntryRequest request, CancellationToken cancellationToken = default);
    Task DeleteAsync(Guid workItemId, Guid entryId, CancellationToken cancellationToken = default);
}
