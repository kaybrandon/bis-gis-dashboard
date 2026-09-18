using FluentAssertions;
using GisDashboard.Infrastructure.AiFill;

namespace GisDashboard.Tests;

public sealed class PdfPageImageRendererTests
{
    [Fact]
    public void Empty_page_still_renders_a_png_so_vision_can_run()
    {
        using var stream = new MemoryStream(EmptyPagePdf());
        var images = PdfPageImageRenderer.Render(stream);
        images.Should().HaveCount(1);
        images[0].MediaType.Should().Be("image/png");
        images[0].PageNumber.Should().Be(1);
        images[0].Bytes.Length.Should().BeGreaterThan(32);
        images[0].Bytes[0].Should().Be(0x89);
        images[0].Bytes[1].Should().Be((byte)'P');
    }

    [Fact]
    public void Image_only_page_emits_embedded_page_image()
    {
        using var stream = new MemoryStream(ImageOnlyPdf());
        PdfTextExtractor.IsUsable(PdfTextExtractor.Extract(new MemoryStream(ImageOnlyPdf()))).Should().BeFalse();
        var images = PdfPageImageRenderer.Render(stream);
        images.Should().HaveCount(1);
        images[0].Bytes.Length.Should().BeGreaterThan(32);
        images[0].MediaType.Should().Be("image/png");
    }

    [Theory]
    [InlineData(null, false)]
    [InlineData("", false)]
    [InlineData("   ", false)]
    [InlineData("x", false)]
    [InlineData("N-14-042 Final Plat of the Northridge Addition, Block 4, Lot 12.", true)]
    public void Usable_text_keeps_the_fast_path(string? text, bool usable)
    {
        PdfTextExtractor.IsUsable(text).Should().Be(usable);
    }

    private static byte[] EmptyPagePdf()
    {
        var sb = new System.Text.StringBuilder();
        sb.Append("%PDF-1.4\n");
        var offsets = new List<int>();
        void Obj(string body)
        {
            offsets.Add(sb.Length);
            sb.Append(body);
            if (!body.EndsWith('\n')) sb.Append('\n');
        }

        Obj("1 0 obj << /Type /Catalog /Pages 2 0 R >> endobj\n");
        Obj("2 0 obj << /Type /Pages /Kids [3 0 R] /Count 1 >> endobj\n");
        Obj("3 0 obj << /Type /Page /Parent 2 0 R /MediaBox [0 0 612 792] >> endobj\n");
        var xref = sb.Length;
        sb.Append($"xref\n0 {offsets.Count + 1}\n0000000000 65535 f \n");
        foreach (var offset in offsets)
        {
            sb.Append($"{offset:D10} 00000 n \n");
        }

        sb.Append($"trailer << /Size {offsets.Count + 1} /Root 1 0 R >>\nstartxref\n{xref}\n%%EOF\n");
        return System.Text.Encoding.ASCII.GetBytes(sb.ToString());
    }

    private static byte[] ImageOnlyPdf()
    {
        var pixels = new byte[8 * 8 * 3];
        Array.Fill(pixels, (byte)0x33);
        var content = "q 200 0 0 200 0 0 cm /Im0 Do Q";
        var sb = new System.Text.StringBuilder();
        sb.Append("%PDF-1.4\n");
        var offsets = new List<int>();
        void Obj(string body)
        {
            offsets.Add(sb.Length);
            sb.Append(body);
            if (!body.EndsWith('\n')) sb.Append('\n');
        }

        Obj("1 0 obj << /Type /Catalog /Pages 2 0 R >> endobj\n");
        Obj("2 0 obj << /Type /Pages /Kids [3 0 R] /Count 1 >> endobj\n");
        Obj("3 0 obj << /Type /Page /Parent 2 0 R /MediaBox [0 0 200 200] /Resources << /XObject << /Im0 4 0 R >> >> /Contents 5 0 R >> endobj\n");
        offsets.Add(sb.Length);
        sb.Append($"4 0 obj << /Type /XObject /Subtype /Image /Width 8 /Height 8 /ColorSpace /DeviceRGB /BitsPerComponent 8 /Length {pixels.Length} >> stream\n");
        sb.Append(System.Text.Encoding.Latin1.GetString(pixels));
        sb.Append("\nendstream endobj\n");
        Obj($"5 0 obj << /Length {content.Length} >> stream\n{content}\nendstream endobj\n");
        var xref = sb.Length;
        sb.Append($"xref\n0 {offsets.Count + 1}\n0000000000 65535 f \n");
        foreach (var offset in offsets)
        {
            sb.Append($"{offset:D10} 00000 n \n");
        }

        sb.Append($"trailer << /Size {offsets.Count + 1} /Root 1 0 R >>\nstartxref\n{xref}\n%%EOF\n");
        return System.Text.Encoding.Latin1.GetBytes(sb.ToString());
    }
}
