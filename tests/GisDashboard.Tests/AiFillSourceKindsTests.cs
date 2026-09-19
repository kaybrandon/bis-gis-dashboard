using FluentAssertions;
using GisDashboard.Application.AiFill;

namespace GisDashboard.Tests;

public sealed class AiFillSourceKindsTests
{
    [Theory]
    [InlineData("plat.pdf", "application/pdf", AiFillSourceKind.Pdf)]
    [InlineData("scan.JPG", "image/jpeg", AiFillSourceKind.Image)]
    [InlineData("photo.jpeg", "image/jpeg", AiFillSourceKind.Image)]
    [InlineData("deed.png", "image/png", AiFillSourceKind.Image)]
    [InlineData("survey.tif", "image/tiff", AiFillSourceKind.Image)]
    [InlineData("survey.tiff", "image/tiff", AiFillSourceKind.Image)]
    [InlineData("notes.docx", "application/vnd.openxmlformats-officedocument.wordprocessingml.document", AiFillSourceKind.Docx)]
    [InlineData("index.xlsx", "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", AiFillSourceKind.Xlsx)]
    public void Supported_matrix_is_analyzable(string fileName, string contentType, AiFillSourceKind kind)
    {
        AiFillSourceKinds.Resolve(fileName, contentType).Should().Be(kind);
        AiFillSourceKinds.IsAnalyzable(fileName, contentType).Should().BeTrue();
    }

    [Theory]
    [InlineData("legacy.doc", "application/msword")]
    [InlineData("legacy.xls", "application/vnd.ms-excel")]
    [InlineData("macro.docm", "application/vnd.ms-word.document.macroEnabled.12")]
    [InlineData("macro.xlsm", "application/vnd.ms-excel.sheet.macroEnabled.12")]
    [InlineData("anim.gif", "image/gif")]
    [InlineData("photo.webp", "image/webp")]
    public void Doc_xls_macros_and_other_images_are_not_analyzed(string fileName, string contentType)
    {
        AiFillSourceKinds.Resolve(fileName, contentType).Should().Be(AiFillSourceKind.Unsupported);
        AiFillSourceKinds.IsAnalyzable(fileName, contentType).Should().BeFalse();
        AiFillSourceKinds.UnsupportedMessage.Should().Contain("XLSX");
        AiFillSourceKinds.UnsupportedMessage.Should().NotContain("DOC,");
    }
}
