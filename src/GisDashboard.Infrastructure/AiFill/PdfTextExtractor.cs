using System.Text;
using UglyToad.PdfPig;
using UglyToad.PdfPig.Content;

namespace GisDashboard.Infrastructure.AiFill;

public static class PdfTextExtractor
{
    public const int DefaultMaxPages = 20;
    public const int DefaultMaxChars = 24_000;

    public static string Extract(Stream stream, int maxPages = DefaultMaxPages, int maxChars = DefaultMaxChars)
    {
        ArgumentNullException.ThrowIfNull(stream);
        using var document = PdfDocument.Open(stream);
        if (document.NumberOfPages < 1)
        {
            return string.Empty;
        }

        var pages = Math.Min(document.NumberOfPages, Math.Max(1, maxPages));
        var builder = new StringBuilder();
        for (var i = 1; i <= pages; i++)
        {
            Page page;
            try
            {
                page = document.GetPage(i);
            }
            catch
            {
                continue;
            }

            var text = page.Text;
            if (string.IsNullOrWhiteSpace(text))
            {
                continue;
            }

            if (builder.Length > 0)
            {
                builder.AppendLine();
            }

            builder.Append(text.Trim());
            if (builder.Length >= maxChars)
            {
                break;
            }
        }

        var extracted = builder.ToString().Trim();
        return extracted.Length <= maxChars ? extracted : extracted[..maxChars];
    }

    public static bool IsUsable(string? text)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return false;
        }

        var trimmed = text.Trim();
        if (trimmed.Length < 40)
        {
            return false;
        }

        var letters = trimmed.Count(char.IsLetter);
        if (letters < 24)
        {
            return false;
        }

        return letters / (double)trimmed.Length >= 0.35;
    }
}
