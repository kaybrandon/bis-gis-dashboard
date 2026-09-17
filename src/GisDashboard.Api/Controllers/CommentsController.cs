using GisDashboard.Application.WorkItems;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace GisDashboard.Api.Controllers;

[ApiController]
[Authorize]
[Route("api/work-items/{workItemId:guid}/comments")]
public sealed class CommentsController : ControllerBase
{
    private readonly ICommentService _comments;

    public CommentsController(ICommentService comments)
    {
        _comments = comments;
    }

    [HttpGet]
    public Task<IReadOnlyList<CommentDto>> List(Guid workItemId, CancellationToken cancellationToken) =>
        _comments.ListAsync(workItemId, cancellationToken);

    [HttpPost]
    public async Task<ActionResult<CommentDto>> Create(
        Guid workItemId,
        [FromBody] CreateCommentRequest request,
        CancellationToken cancellationToken)
    {
        var created = await _comments.CreateAsync(workItemId, request, cancellationToken);
        return Created($"/api/work-items/{workItemId}/comments/{created.Id}", created);
    }
}
