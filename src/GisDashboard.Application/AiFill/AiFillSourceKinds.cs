namespace GisDashboard.Application.AiFill;

public enum AiFillSourceKind
{
    Unsupported,
    Pdf,
    Image,
    Docx,
    Xlsx
}

/// <summary>
/// QC4-05 analysis matrix: PDF, JPG/JPEG, PNG, TIFF/TIF, DOCX, XLSX (first sheet).
/// DOC, XLS, macros/v1, GIF, and WebP stay uploadable but are not analyzed.
/// </summary>
public static class AiFillSourceKinds
{
    public const string SupportedLabel = "PDF, JPG/JPEG, PNG, TIFF/TIF, DOCX, and XLSX (first sheet)";

    public const string UnsupportedMessage =
        $"AI fill supports {SupportedLabel}. This file type is not analyzed.";

    public const string PasswordMessage =
        "This file is password-protected or encrypted. Remove the password and upload again, or type the fields.";

    public const string UnreadableMessage =
        "This file could not be read. Replace it with a supported file, or type the fields.";

    public const string EmptyOfficeMessage =
        "This Office file has no readable text. Replace it with a text DOCX or XLSX (first sheet), or type the fields.";

    public const string EmptyImageMessage =
        "This image could not be converted for AI fill. Replace it, or type the fields.";

    public static AiFillSourceKind Resolve(string? fileName, string? contentType)
    {
        var ext = Path.GetExtension(fileName) ?? string.Empty;
        var mime = Normalize(contentType);

        if (ext.Equals(".pdf", StringComparison.OrdinalIgnoreCase)
            || mime == "application/pdf")
        {
            return AiFillSourceKind.Pdf;
        }

        if (ext.Equals(".docx", StringComparison.OrdinalIgnoreCase)
            || mime == "application/vnd.openxmlformats-officedocument.wordprocessingml.document")
        {
            return AiFillSourceKind.Docx;
        }

        if (ext.Equals(".xlsx", StringComparison.OrdinalIgnoreCase)
            || mime == "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet")
        {
            return AiFillSourceKind.Xlsx;
        }

        if (IsAnalyzableImage(ext, mime))
        {
            return AiFillSourceKind.Image;
        }

        return AiFillSourceKind.Unsupported;
    }

    public static bool IsAnalyzable(string? fileName, string? contentType) =>
        Resolve(fileName, contentType) is not AiFillSourceKind.Unsupported;

    public static string KindLabel(AiFillSourceKind kind) => kind switch
    {
        AiFillSourceKind.Pdf => "PDF",
        AiFillSourceKind.Image => "image",
        AiFillSourceKind.Docx => "DOCX",
        AiFillSourceKind.Xlsx => "XLSX",
        _ => "file"
    };

    private static bool IsAnalyzableImage(string ext, string mime)
    {
        if (ext.Equals(".jpg", StringComparison.OrdinalIgnoreCase)
            || ext.Equals(".jpeg", StringComparison.OrdinalIgnoreCase)
            || ext.Equals(".png", StringComparison.OrdinalIgnoreCase)
            || ext.Equals(".tif", StringComparison.OrdinalIgnoreCase)
            || ext.Equals(".tiff", StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        return mime is "image/jpeg" or "image/jpg" or "image/png" or "image/tiff" or "image/tif" or "image/x-tiff";
    }

    private static string Normalize(string? contentType)
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

        return mime.ToLowerInvariant();
    }
}
