using GisDashboard.Application.AiFill;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Formats.Png;
using SixLabors.ImageSharp.Processing;

namespace GisDashboard.Infrastructure.AiFill;

/// <summary>
/// Rasterizes JPG/JPEG, PNG, and TIFF/TIF pages for the vision AI-fill path.
/// Reuses the PDF vision page/edge caps. No Document Intelligence / OCR pipeline.
/// </summary>
public static class DocumentImageRenderer
{
    public const int DefaultMaxPages = PdfPageImageRenderer.DefaultMaxPages;
    public const int MaxEdge = PdfPageImageRenderer.MaxEdge;

    public static IReadOnlyList<AiFillVisionImage> Render(Stream stream, int maxPages = DefaultMaxPages)
    {
        ArgumentNullException.ThrowIfNull(stream);
        try
        {
            using var image = Image.Load(stream);
            var frames = Math.Min(Math.Max(1, image.Frames.Count), Math.Max(1, maxPages));
            var result = new List<AiFillVisionImage>(frames);
            for (var i = 0; i < frames; i++)
            {
                using var frame = image.Frames.CloneFrame(i);
                if (frame.Width > MaxEdge || frame.Height > MaxEdge)
                {
                    frame.Mutate(x => x.Resize(new ResizeOptions
                    {
                        Size = new Size(MaxEdge, MaxEdge),
                        Mode = ResizeMode.Max
                    }));
                }

                using var output = new MemoryStream();
                frame.Save(output, new PngEncoder());
                var png = output.ToArray();
                if (png.Length > 0)
                {
                    result.Add(new AiFillVisionImage(png, "image/png", i + 1));
                }
            }

            return result;
        }
        catch (Exception ex) when (ex is ImageFormatException or NotSupportedException or InvalidImageContentException)
        {
            throw new InvalidDataException(AiFillSourceKinds.EmptyImageMessage, ex);
        }
    }
}
