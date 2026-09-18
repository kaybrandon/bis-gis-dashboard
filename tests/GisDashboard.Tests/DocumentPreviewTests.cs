using FluentAssertions;
using GisDashboard.Application.Exceptions;
using GisDashboard.Application.WorkItems;
using GisDashboard.Infrastructure.Preview;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Formats.Tiff;
using SixLabors.ImageSharp.PixelFormats;

namespace GisDashboard.Tests;

public sealed class DocumentPreviewTests
{
    [Theory]
    [InlineData("scan.tif", "application/octet-stream")]
    [InlineData("scan.tiff", "")]
    [InlineData("scan", "image/tiff")]
    [InlineData("plat.TIF", "image/x-tiff")]
    public void Detects_tiff_by_extension_or_mime(string fileName, string contentType)
    {
        DocumentPreview.IsTiff(fileName, contentType).Should().BeTrue();
        DocumentPreview.IsBrowserImage(fileName, contentType).Should().BeFalse();
    }

    [Theory]
    [InlineData("photo.jpg", "image/jpeg")]
    [InlineData("photo.jpeg", "")]
    [InlineData("scan.png", "image/png")]
    [InlineData("anim.gif", "image/gif")]
    [InlineData("shot.webp", "image/webp")]
    public void Browser_images_are_not_treated_as_tiff(string fileName, string contentType)
    {
        DocumentPreview.IsTiff(fileName, contentType).Should().BeFalse();
        DocumentPreview.IsBrowserImage(fileName, contentType).Should().BeTrue();
    }

    [Fact]
    public void Unavailable_and_more_pages_messages_are_explicit()
    {
        DocumentPreview.UnavailableMessage.Should().Contain("Preview is unavailable");
        DocumentPreview.TiffUnavailableMessage.Should().Contain("Preview is unavailable");
        DocumentPreview.TiffUnavailableMessage.Should().Contain("TIFF");
        DocumentPreview.MorePagesNote(1).Should().BeEmpty();
        DocumentPreview.MorePagesNote(3).Should().Be("This TIFF has 3 pages. Showing the first page.");
    }

    [Fact]
    public async Task Rasterize_returns_png_of_first_page_and_notes_extra_pages()
    {
        await using var tiff = await TiffPreviewFixtures.MultiPageTiffAsync(Color.Red, Color.Blue);
        var preview = await TiffFirstPagePreview.RasterizeAsync(tiff);
        preview.ContentType.Should().Be("image/png");
        preview.Kind.Should().Be(DocumentPreview.TiffKind);
        preview.PageCount.Should().Be(2);
        preview.MorePages.Should().BeTrue();
        preview.FileName.Should().Be("preview.png");

        preview.Content.Position = 0;
        using var rendered = await Image.LoadAsync<Rgba32>(preview.Content);
        rendered.Frames.Count.Should().Be(1);
        rendered[0, 0].R.Should().BeGreaterThan(200);
        await preview.Content.DisposeAsync();
    }

    [Fact]
    public async Task Rasterize_single_page_tiff_has_no_more_pages_flag()
    {
        await using var tiff = await TiffPreviewFixtures.SinglePageTiffAsync(Color.Lime);
        var preview = await TiffFirstPagePreview.RasterizeAsync(tiff);
        preview.PageCount.Should().Be(1);
        preview.MorePages.Should().BeFalse();
        await preview.Content.DisposeAsync();
    }

    [Fact]
    public async Task Rasterize_rejects_garbage_with_unavailable_message()
    {
        await using var garbage = new MemoryStream("not-a-tiff"u8.ToArray());
        var act = async () => await TiffFirstPagePreview.RasterizeAsync(garbage);
        var ex = await act.Should().ThrowAsync<ValidationException>();
        ex.Which.Message.Should().Be(DocumentPreview.TiffUnavailableMessage);
    }
}

internal static class TiffPreviewFixtures
{
    public static async Task<MemoryStream> SinglePageTiffAsync(Color color)
    {
        using var image = new Image<Rgba32>(8, 8);
        Fill(image, color);
        var stream = new MemoryStream();
        await image.SaveAsync(stream, new TiffEncoder());
        stream.Position = 0;
        return stream;
    }

    public static async Task<MemoryStream> MultiPageTiffAsync(Color first, Color second)
    {
        using var image = new Image<Rgba32>(8, 8);
        Fill(image, first);

        using var page2 = new Image<Rgba32>(8, 8);
        Fill(page2, second);
        image.Frames.AddFrame(page2.Frames.RootFrame);

        var stream = new MemoryStream();
        await image.SaveAsync(stream, new TiffEncoder());
        stream.Position = 0;
        return stream;
    }

    public static async Task<byte[]> JpegBytesAsync()
    {
        using var image = new Image<Rgba32>(8, 8);
        Fill(image, Color.Yellow);
        var stream = new MemoryStream();
        await image.SaveAsJpegAsync(stream);
        return stream.ToArray();
    }

    private static void Fill(Image<Rgba32> image, Color color)
    {
        var pixel = color.ToPixel<Rgba32>();
        image.ProcessPixelRows(accessor =>
        {
            for (var y = 0; y < accessor.Height; y++)
            {
                accessor.GetRowSpan(y).Fill(pixel);
            }
        });
    }
}
