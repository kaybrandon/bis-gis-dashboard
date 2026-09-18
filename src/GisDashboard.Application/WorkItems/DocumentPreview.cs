namespace GisDashboard.Application.WorkItems;

/// <summary>
/// CR04: TIFF is a supported upload image, but browsers cannot render it in
/// <c>&lt;img&gt;</c>. Preview rasterizes the first page. JPEG/PNG/GIF/WebP stay
/// browser-native. Word/Excel stay download-only.
/// </summary>
public static class DocumentPreview
{
    public const string UnavailableMessage =
        "Preview is unavailable for this file. Download the original instead.";

    public const string TiffUnavailableMessage =
        "Preview is unavailable for this TIFF. Download the original instead.";

    public const string BrowserImageKind = "image";
    public const string TiffKind = "tiff";

    public static string MorePagesNote(int pageCount) =>
        pageCount > 1
            ? $"This TIFF has {pageCount} pages. Showing the first page."
            : string.Empty;

    public static bool IsTiff(string? fileName, string? contentType)
    {
        var mime = NormalizeContentType(contentType);
        if (mime is "image/tiff" or "image/tif" or "image/x-tiff")
        {
            return true;
        }

        var ext = Path.GetExtension(fileName) ?? string.Empty;
        return ext.Equals(".tif", StringComparison.OrdinalIgnoreCase)
            || ext.Equals(".tiff", StringComparison.OrdinalIgnoreCase);
    }

    public static bool IsBrowserImage(string? fileName, string? contentType)
    {
        if (IsTiff(fileName, contentType))
        {
            return false;
        }

        var mime = NormalizeContentType(contentType);
        if (mime.StartsWith("image/", StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        var ext = Path.GetExtension(fileName) ?? string.Empty;
        return ext.Equals(".png", StringComparison.OrdinalIgnoreCase)
            || ext.Equals(".jpg", StringComparison.OrdinalIgnoreCase)
            || ext.Equals(".jpeg", StringComparison.OrdinalIgnoreCase)
            || ext.Equals(".gif", StringComparison.OrdinalIgnoreCase)
            || ext.Equals(".webp", StringComparison.OrdinalIgnoreCase);
    }

    public static bool IsPdf(string? fileName, string? contentType)
    {
        var mime = NormalizeContentType(contentType);
        if (mime.Contains("pdf", StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        return (Path.GetExtension(fileName) ?? string.Empty).Equals(".pdf", StringComparison.OrdinalIgnoreCase);
    }

    private static string NormalizeContentType(string? contentType)
    {
        if (string.IsNullOrWhiteSpace(contentType))
        {
            return string.Empty;
        }

        var mime = contentType.Trim();
        var separator = mime.IndexOf(';');
        return separator >= 0 ? mime[..separator].Trim().ToLowerInvariant() : mime.ToLowerInvariant();
    }
}
