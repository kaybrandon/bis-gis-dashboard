namespace GisDashboard.Application.WorkItems;

public interface ICommentService
{
    Task<IReadOnlyList<CommentDto>> ListAsync(Guid workItemId, CancellationToken cancellationToken = default);
    Task<CommentDto> CreateAsync(Guid workItemId, CreateCommentRequest request, CancellationToken cancellationToken = default);
}
