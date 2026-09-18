namespace GisDashboard.Application.WorkItems;

/// <summary>
/// Shared work-queue upload types. Word and Excel are upload + download only — no Office preview.
/// </summary>
public static class UploadFileTypes
{
    public const string SupportedTypesLabel = "PDF, Word (.doc, .docx), Excel (.xls, .xlsx), or images";

    public const string RequiredFileMessage = "Attach at least one PDF, Word, Excel, or image.";

    public const string UnsupportedMessage =
        "Upload a PDF, Word (.doc, .docx), Excel (.xls, .xlsx), or image. That file type is not supported.";

    public const string AcceptAttribute =
        ".pdf,.doc,.docx,.xls,.xlsx,.png,.jpg,.jpeg,.gif,.webp,.tif,.tiff," +
        "application/pdf,application/msword," +
        "application/vnd.openxmlformats-officedocument.wordprocessingml.document," +
        "application/vnd.ms-excel," +
        "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet," +
        "image/png,image/jpeg,image/gif,image/webp,image/tiff";

    public static readonly IReadOnlyList<string> Extensions =
    [
        ".pdf",
        ".doc",
        ".docx",
        ".xls",
        ".xlsx",
        ".png",
        ".jpg",
        ".jpeg",
        ".gif",
        ".webp",
        ".tif",
        ".tiff"
    ];

    private static readonly Dictionary<string, string> ContentTypeByExtension = new(StringComparer.OrdinalIgnoreCase)
    {
        [".pdf"] = "application/pdf",
        [".doc"] = "application/msword",
        [".docx"] = "application/vnd.openxmlformats-officedocument.wordprocessingml.document",
        [".xls"] = "application/vnd.ms-excel",
        [".xlsx"] = "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
        [".png"] = "image/png",
        [".jpg"] = "image/jpeg",
        [".jpeg"] = "image/jpeg",
        [".gif"] = "image/gif",
        [".webp"] = "image/webp",
        [".tif"] = "image/tiff",
        [".tiff"] = "image/tiff"
    };

    private static readonly HashSet<string> AllowedContentTypes = new(StringComparer.OrdinalIgnoreCase)
    {
        "application/pdf",
        "application/msword",
        "application/vnd.openxmlformats-officedocument.wordprocessingml.document",
        "application/vnd.ms-excel",
        "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
        "image/png",
        "image/jpeg",
        "image/jpg",
        "image/gif",
        "image/webp",
        "image/tiff"
    };

    public static bool TryResolve(string? fileName, string? contentType, out string resolvedContentType)
    {
        var ext = Path.GetExtension(fileName);
        if (!string.IsNullOrWhiteSpace(ext) && ContentTypeByExtension.TryGetValue(ext, out resolvedContentType!))
        {
            return true;
        }

        var mime = NormalizeContentType(contentType);
        if (mime.Length > 0 && AllowedContentTypes.Contains(mime))
        {
            resolvedContentType = mime.Equals("image/jpg", StringComparison.OrdinalIgnoreCase) ? "image/jpeg" : mime;
            return true;
        }

        resolvedContentType = mime.Length > 0 ? mime : "application/octet-stream";
        return false;
    }

    public static string UnsupportedFileMessage(string? fileName)
    {
        var name = string.IsNullOrWhiteSpace(fileName) ? "This file" : Path.GetFileName(fileName);
        return $"{name} is not a supported type. Upload a {SupportedTypesLabel}.";
    }

    private static string NormalizeContentType(string? contentType)
    {
        if (string.IsNullOrWhiteSpace(contentType))
        {
            return string.Empty;
        }

        var mime = contentType.Trim();
        var separator = mime.IndexOf(';');
        if (separator >= 0)
        {
            mime = mime[..separator].Trim();
        }

        return mime;
    }
}
