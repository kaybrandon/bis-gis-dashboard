using FluentAssertions;
using GisDashboard.Application.AiFill;
using GisDashboard.Infrastructure.AiFill;
using SixLabors.ImageSharp;

namespace GisDashboard.Tests;

public sealed class DocumentImageRendererTests
{
    [Fact]
    public async Task Png_and_jpeg_render_a_vision_page()
    {
        var png = await OfficeAiFillFixtures.PngBytesAsync();
        using var pngStream = new MemoryStream(png);
        var pngPages = DocumentImageRenderer.Render(pngStream);
        pngPages.Should().HaveCount(1);
        pngPages[0].MediaType.Should().Be("image/png");
        pngPages[0].Bytes[0].Should().Be(0x89);

        var jpeg = await OfficeAiFillFixtures.JpegBytesAsync();
        using var jpegStream = new MemoryStream(jpeg);
        var jpegPages = DocumentImageRenderer.Render(jpegStream);
        jpegPages.Should().HaveCount(1);
        jpegPages[0].PageNumber.Should().Be(1);
    }

    [Fact]
    public async Task Multi_page_tiff_stays_within_the_existing_vision_page_cap()
    {
        await using var tiff = await TiffPreviewFixtures.MultiPageTiffAsync(Color.Red, Color.Blue);
        var pages = DocumentImageRenderer.Render(tiff);
        pages.Should().HaveCount(2);
        pages[0].PageNumber.Should().Be(1);
        pages[1].PageNumber.Should().Be(2);
        pages.Should().OnlyContain(page => page.MediaType == "image/png" && page.Bytes.Length > 32);
    }

    [Fact]
    public void Unreadable_image_fails_closed()
    {
        using var stream = new MemoryStream("not-an-image"u8.ToArray());
        var act = () => DocumentImageRenderer.Render(stream);
        act.Should().Throw<InvalidDataException>().WithMessage(AiFillSourceKinds.EmptyImageMessage);
    }
}
