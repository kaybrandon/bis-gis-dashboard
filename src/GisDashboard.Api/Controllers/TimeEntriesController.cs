using GisDashboard.Application.WorkItems;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace GisDashboard.Api.Controllers;

[ApiController]
[Authorize]
[Route("api/work-items/{workItemId:guid}/time-entries")]
public sealed class TimeEntriesController : ControllerBase
{
    private readonly ITimeEntryService _timeEntries;

    public TimeEntriesController(ITimeEntryService timeEntries)
    {
        _timeEntries = timeEntries;
    }

    [HttpGet]
    public Task<TimeEntryListResponse> List(Guid workItemId, CancellationToken cancellationToken) =>
        _timeEntries.ListAsync(workItemId, cancellationToken);

    [HttpPost]
    public async Task<ActionResult<TimeEntryDto>> Create(
        Guid workItemId,
        [FromBody] UpsertTimeEntryRequest request,
        CancellationToken cancellationToken)
    {
        var created = await _timeEntries.CreateAsync(workItemId, request, cancellationToken);
        return Created($"/api/work-items/{workItemId}/time-entries/{created.Id}", created);
    }

    [HttpPatch("{entryId:guid}")]
    public Task<TimeEntryDto> Update(
        Guid workItemId,
        Guid entryId,
        [FromBody] UpsertTimeEntryRequest request,
        CancellationToken cancellationToken) =>
        _timeEntries.UpdateAsync(workItemId, entryId, request, cancellationToken);

    [HttpDelete("{entryId:guid}")]
    public async Task<IActionResult> Delete(Guid workItemId, Guid entryId, CancellationToken cancellationToken)
    {
        await _timeEntries.DeleteAsync(workItemId, entryId, cancellationToken);
        return NoContent();
    }
}
