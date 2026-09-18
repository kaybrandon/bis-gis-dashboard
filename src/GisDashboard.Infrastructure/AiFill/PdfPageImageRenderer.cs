using GisDashboard.Application.AiFill;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Formats.Png;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Processing;
using UglyToad.PdfPig;
using UglyToad.PdfPig.Content;

namespace GisDashboard.Infrastructure.AiFill;

/// <summary>
/// Renders PDF pages to PNG for the vision AI-fill path. Prefers embedded page
/// images (typical scanned / image-only deeds), then a white page canvas so
/// vision is still attempted. No Document Intelligence / OCR pipeline.
/// </summary>
public static class PdfPageImageRenderer
{
    public const int DefaultMaxPages = 8;
    public const int MaxEdge = 1600;

    public static IReadOnlyList<AiFillVisionImage> Render(Stream stream, int maxPages = DefaultMaxPages)
    {
        ArgumentNullException.ThrowIfNull(stream);
        using var document = PdfDocument.Open(stream);
        if (document.NumberOfPages < 1)
        {
            return [];
        }

        var pages = Math.Min(document.NumberOfPages, Math.Max(1, maxPages));
        var result = new List<AiFillVisionImage>(pages);
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

            var png = RenderPage(page);
            if (png is { Length: > 0 })
            {
                result.Add(new AiFillVisionImage(png, "image/png", i));
            }
        }

        return result;
    }

    private static byte[] RenderPage(Page page)
    {
        byte[]? best = null;
        var bestArea = 0;
        foreach (var pdfImage in page.GetImages())
        {
            if (pdfImage.IsImageMask)
            {
                continue;
            }

            var png = DecodeToPng(pdfImage);
            if (png is null)
            {
                continue;
            }

            var area = Math.Max(1, pdfImage.WidthInSamples) * Math.Max(1, pdfImage.HeightInSamples);
            if (area >= bestArea)
            {
                bestArea = area;
                best = png;
            }
        }

        return best ?? BlankPagePng(page);
    }

    private static byte[]? DecodeToPng(IPdfImage pdfImage)
    {
        try
        {
            if (pdfImage.TryGetPng(out var png) && png is { Length: > 32 })
            {
                return NormalizePng(png);
            }
        }
        catch
        {
            // Fall through to raw / decoded bytes.
        }

        if (TryNormalize(ToArray(pdfImage.RawBytes), out var fromRaw))
        {
            return fromRaw;
        }

        try
        {
            if (pdfImage.TryGetBytes(out var decoded)
                && decoded is { Count: > 0 }
                && TryNormalize(ToArray(decoded), out var fromDecoded))
            {
                return fromDecoded;
            }
        }
        catch
        {
            // Some filters throw; the blank-page fallback still lets vision run.
        }

        return null;
    }

    private static byte[] BlankPagePng(Page page)
    {
        var width = Math.Clamp((int)Math.Round(page.Width), 200, MaxEdge);
        var height = Math.Clamp((int)Math.Round(page.Height), 200, MaxEdge);
        using var image = new Image<Rgba32>(width, height, Color.White);
        return EncodePng(image);
    }

    private static byte[]? NormalizePng(byte[] png)
    {
        return TryNormalize(png, out var normalized) ? normalized : png;
    }

    private static bool TryNormalize(byte[]? bytes, out byte[] png)
    {
        png = [];
        if (bytes is not { Length: > 8 })
        {
            return false;
        }

        try
        {
            using var image = Image.Load(bytes);
            if (image.Width > MaxEdge || image.Height > MaxEdge)
            {
                image.Mutate(x => x.Resize(new ResizeOptions
                {
                    Size = new Size(MaxEdge, MaxEdge),
                    Mode = ResizeMode.Max
                }));
            }

            png = EncodePng(image);
            return png.Length > 0;
        }
        catch
        {
            return false;
        }
    }

    private static byte[] EncodePng(Image image)
    {
        using var output = new MemoryStream();
        image.Save(output, new PngEncoder());
        return output.ToArray();
    }

    private static byte[] ToArray(IReadOnlyList<byte> bytes)
    {
        if (bytes is byte[] array)
        {
            return array;
        }

        var copy = new byte[bytes.Count];
        for (var i = 0; i < bytes.Count; i++)
        {
            copy[i] = bytes[i];
        }

        return copy;
    }
}
