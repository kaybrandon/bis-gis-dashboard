using GisDashboard.Application.AiFill;
using GisDashboard.Application.WorkItems;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace GisDashboard.Api.Controllers;

[ApiController]
[Authorize]
[Route("api/work-items")]
public sealed class WorkItemsController : ControllerBase
{
    private readonly IWorkItemService _workItems;
    private readonly IWorkItemAiFillService _aiFill;

    public WorkItemsController(IWorkItemService workItems, IWorkItemAiFillService aiFill)
    {
        _workItems = workItems;
        _aiFill = aiFill;
    }

    [HttpGet]
    public Task<WorkItemListResponse> List([FromQuery] WorkItemQuery query, CancellationToken cancellationToken) =>
        _workItems.ListAsync(query, cancellationToken);

    [HttpGet("export")]
    public async Task<IActionResult> Export([FromQuery] WorkItemQuery query, CancellationToken cancellationToken)
    {
        var file = await _workItems.ExportAsync(query, cancellationToken);
        return File(file.Content, file.ContentType, file.FileName);
    }

    [HttpGet("{id:guid}")]
    public Task<WorkItemDetail> Get(Guid id, CancellationToken cancellationToken) =>
        _workItems.GetAsync(id, cancellationToken);

    [HttpGet("{id:guid}/neighbors")]
    public Task<WorkItemNeighbors> Neighbors(Guid id, [FromQuery] WorkItemQuery query, CancellationToken cancellationToken) =>
        _workItems.GetNeighborsAsync(id, query, cancellationToken);

    [HttpGet("{id:guid}/file")]
    public async Task<IActionResult> File(Guid id, CancellationToken cancellationToken)
    {
        var download = await _workItems.OpenFileAsync(id, cancellationToken);
        return File(download.Content, download.ContentType, download.FileName, enableRangeProcessing: true);
    }

    [HttpGet("{id:guid}/preview")]
    public async Task<IActionResult> Preview(Guid id, CancellationToken cancellationToken)
    {
        var preview = await _workItems.OpenPreviewAsync(id, cancellationToken);
        Response.Headers["X-Preview-Page"] = "1";
        Response.Headers["X-Preview-Page-Count"] = preview.PageCount.ToString();
        Response.Headers["X-Preview-Kind"] = preview.Kind;
        if (preview.MorePages)
        {
            Response.Headers["X-Preview-More-Pages"] = "true";
        }

        // No fileDownloadName — Content-Disposition stays inline so the viewer
        // can render JPEG/PNG/TIFF-preview without forcing a download.
        return File(preview.Content, preview.ContentType, enableRangeProcessing: true);
    }

    [HttpPost]
    [RequestSizeLimit(UploadOptions.HttpRequestCeilingBytes)]
    [Consumes("multipart/form-data")]
    public async Task<WorkItemDetail> Upload([FromForm] UploadForm form, CancellationToken cancellationToken)
    {
        if (form.File is null || form.File.Length == 0)
        {
            throw new Application.Exceptions.ValidationException("A file is required.");
        }

        await using var stream = form.File.OpenReadStream();
        return await _workItems.UploadAsync(new UploadWorkItemRequest
        {
            OrganizationId = ParseGuid(FirstNonEmpty(form.OrganizationId, FormValue("organizationId"), FormValue("OrganizationId"))),
            OrganizationName = FirstNonEmpty(form.OrganizationName, form.Organization, FormValue("organizationName"), FormValue("organization")),
            DocumentTypeId = ParseGuid(FirstNonEmpty(form.DocumentTypeId, FormValue("documentTypeId"), FormValue("DocumentTypeId"))),
            DocumentTypeName = FirstNonEmpty(form.DocumentTypeName, FormValue("documentTypeName"), FormValue("documentType")),
            Title = form.Title,
            AssignedToUserId = ParseOptionalGuid(form.AssignedToUserId),
            IsPriority = form.IsPriority,
            PriorityNote = form.PriorityNote,
            ClientNotes = form.ClientNotes,
            FileName = form.File.FileName,
            ContentType = form.File.ContentType,
            Content = stream,
            FileSizeBytes = form.File.Length
        }, cancellationToken);
    }

    [HttpPatch("{id:guid}")]
    public Task<WorkItemDetail> Update(Guid id, [FromBody] UpdateWorkItemRequest request, CancellationToken cancellationToken) =>
        _workItems.UpdateAsync(id, request, cancellationToken);

    [HttpPost("{id:guid}/ai-fill")]
    public Task<AiFillResponse> AiFill(
        Guid id,
        [FromQuery] bool rescore,
        CancellationToken cancellationToken) =>
        _aiFill.FillFromPdfAsync(id, rescore, cancellationToken);

    private string? FormValue(string key)
    {
        if (Request.HasFormContentType && Request.Form.TryGetValue(key, out var value))
        {
            return value.ToString();
        }

        return null;
    }

    private static string? FirstNonEmpty(params string?[] values) =>
        values.FirstOrDefault(v => !string.IsNullOrWhiteSpace(v))?.Trim();

    private static Guid ParseGuid(string? value) =>
        Guid.TryParse(value, out var id) ? id : Guid.Empty;

    private static Guid? ParseOptionalGuid(string? value) =>
        Guid.TryParse(value, out var id) ? id : null;

    public sealed class UploadForm
    {
        public string? OrganizationId { get; set; }
        public string? OrganizationName { get; set; }
        public string? Organization { get; set; }
        public string? DocumentTypeId { get; set; }
        public string? DocumentTypeName { get; set; }
        public string? Title { get; set; }
        public string? AssignedToUserId { get; set; }
        public bool IsPriority { get; set; }
        public string? PriorityNote { get; set; }
        public string? ClientNotes { get; set; }
        public IFormFile? File { get; set; }
    }
}
