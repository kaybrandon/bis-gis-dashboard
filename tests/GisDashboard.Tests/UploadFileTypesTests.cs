using FluentAssertions;
using GisDashboard.Application.WorkItems;

namespace GisDashboard.Tests;

public sealed class UploadFileTypesTests
{
    [Theory]
    [InlineData("deed.doc", "application/octet-stream", "application/msword")]
    [InlineData("notes.docx", "", "application/vnd.openxmlformats-officedocument.wordprocessingml.document")]
    [InlineData("parcel.xls", "application/vnd.ms-excel", "application/vnd.ms-excel")]
    [InlineData("grid.xlsx", "application/zip", "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet")]
    [InlineData("plat.pdf", "application/pdf", "application/pdf")]
    [InlineData("scan.png", "image/png", "image/png")]
    [InlineData("photo.jpg", "image/jpg", "image/jpeg")]
    public void Resolve_uses_extension_first_for_supported_types(string fileName, string contentType, string expected)
    {
        UploadFileTypes.TryResolve(fileName, contentType, out var resolved).Should().BeTrue();
        resolved.Should().Be(expected);
    }

    [Fact]
    public void Resolve_accepts_pdf_by_content_type_when_extension_is_missing()
    {
        UploadFileTypes.TryResolve("scan", "application/pdf", out var resolved).Should().BeTrue();
        resolved.Should().Be("application/pdf");
    }

    [Theory]
    [InlineData("malware.exe", "application/octet-stream")]
    [InlineData("bundle.zip", "application/zip")]
    [InlineData("notes.txt", "text/plain")]
    public void Resolve_rejects_unsupported_types(string fileName, string contentType)
    {
        UploadFileTypes.TryResolve(fileName, contentType, out _).Should().BeFalse();
    }

    [Fact]
    public void Help_text_lists_word_excel_and_limits_language()
    {
        UploadFileTypes.SupportedTypesLabel.Should().Contain(".doc");
        UploadFileTypes.SupportedTypesLabel.Should().Contain(".docx");
        UploadFileTypes.SupportedTypesLabel.Should().Contain(".xls");
        UploadFileTypes.SupportedTypesLabel.Should().Contain(".xlsx");
        UploadFileTypes.SupportedTypesLabel.Should().Contain("PDF");
        UploadFileTypes.SupportedTypesLabel.Should().Contain("image");
        UploadFileTypes.SupportedTypesLabel.Should().NotContain("PDFs or images only");
        UploadFileTypes.AcceptAttribute.Should().Contain(".doc");
        UploadFileTypes.AcceptAttribute.Should().Contain(".xlsx");
        UploadFileTypes.UnsupportedFileMessage("old.zip").Should().Contain("old.zip");
        UploadFileTypes.UnsupportedFileMessage("old.zip").Should().Contain(UploadFileTypes.SupportedTypesLabel);
    }
}
