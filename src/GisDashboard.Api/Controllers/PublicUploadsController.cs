using GisDashboard.Application.WorkItems;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace GisDashboard.Api.Controllers;

[ApiController]
[AllowAnonymous]
[EnableRateLimiting("public-upload")]
[Route("api/public/uploads")]
public sealed class PublicUploadsController : ControllerBase
{
    private readonly IWorkItemService _workItems;

    public PublicUploadsController(IWorkItemService workItems)
    {
        _workItems = workItems;
    }

    [HttpGet("{token}")]
    public Task<PublicUploadInfo> Get(string token, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(token))
        {
            throw new Application.Exceptions.NotFoundException("Upload link was not found.");
        }

        return _workItems.GetPublicUploadAsync(token.Trim(), cancellationToken);
    }

    [HttpPost("{token}")]
    [RequestSizeLimit(UploadOptions.HttpRequestCeilingBytes)]
    [Consumes("multipart/form-data")]
    public async Task<PublicUploadResult> Upload(string token, [FromForm] PublicUploadForm form, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(token))
        {
            throw new Application.Exceptions.NotFoundException("Upload link was not found.");
        }

        if (form.File is null || form.File.Length == 0)
        {
            throw new Application.Exceptions.ValidationException("A file is required.");
        }

        await using var stream = form.File.OpenReadStream();
        return await _workItems.UploadByTokenAsync(token.Trim(), new UploadWorkItemRequest
        {
            OrganizationId = Guid.Empty,
            DocumentTypeId = Guid.TryParse(form.DocumentTypeId, out var typeId) ? typeId : Guid.Empty,
            DocumentTypeName = form.DocumentTypeName,
            Title = form.Title,
            IsPriority = form.IsPriority,
            PriorityNote = form.PriorityNote,
            ClientNotes = form.ClientNotes,
            FileName = form.File.FileName,
            ContentType = form.File.ContentType,
            Content = stream,
            FileSizeBytes = form.File.Length
        }, cancellationToken);
    }

    [HttpPost("{token}/received")]
    public async Task<IActionResult> Received(string token, [FromBody] PublicUploadReceivedRequest? request, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(token))
        {
            throw new Application.Exceptions.NotFoundException("Upload link was not found.");
        }

        await _workItems.NotifyPublicUploadReceivedAsync(
            token.Trim(),
            request ?? new PublicUploadReceivedRequest(),
            cancellationToken);
        return Ok(new { accepted = true });
    }

    public sealed class PublicUploadForm
    {
        public string? DocumentTypeId { get; set; }
        public string? DocumentTypeName { get; set; }
        public string? Title { get; set; }
        public bool IsPriority { get; set; }
        public string? PriorityNote { get; set; }
        public string? ClientNotes { get; set; }
        public IFormFile? File { get; set; }
    }
}
