using GisDashboard.Application.Exceptions;
using GisDashboard.Application.WorkItems;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Formats.Png;
using SixLabors.ImageSharp.Processing;

namespace GisDashboard.Infrastructure.Preview;

/// <summary>
/// Rasterizes page 1 of a TIFF to PNG so the item viewer can show it in-browser.
/// Multi-page navigation is out of v1 — only a page-count note is returned.
/// </summary>
public static class TiffFirstPagePreview
{
    public static async Task<FilePreview> RasterizeAsync(Stream source, CancellationToken cancellationToken = default)
    {
        try
        {
            using var image = await Image.LoadAsync(source, cancellationToken);
            var pageCount = Math.Max(1, image.Frames.Count);
            if (image.Frames.Count > 1)
            {
                // Encode only the first page. Extra frames stay in memory only for the count.
                while (image.Frames.Count > 1)
                {
                    image.Frames.RemoveFrame(image.Frames.Count - 1);
                }
            }

            var output = new MemoryStream();
            await image.SaveAsync(output, new PngEncoder(), cancellationToken);
            output.Position = 0;
            return new FilePreview(
                output,
                "image/png",
                "preview.png",
                pageCount,
                pageCount > 1,
                DocumentPreview.TiffKind);
        }
        catch (Exception ex) when (ex is ImageFormatException or NotSupportedException or ImageProcessingException)
        {
            throw new ValidationException(DocumentPreview.TiffUnavailableMessage);
        }
    }
}
