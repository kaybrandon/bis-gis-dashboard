using FluentAssertions;
using GisDashboard.Application.AiFill;
using GisDashboard.Infrastructure.AiFill;

namespace GisDashboard.Tests;

public sealed class OfficeTextExtractorTests
{
    [Fact]
    public void Docx_extracts_the_actual_document_text()
    {
        var bytes = OfficeAiFillFixtures.TextDocx("NORTHRIDGE WARRANTY DEED LOT 12 BLOCK 4");
        using var stream = new MemoryStream(bytes);
        var text = OfficeTextExtractor.ExtractDocx(stream);
        text.Should().Contain("NORTHRIDGE WARRANTY DEED LOT 12");
        OfficeTextExtractor.IsUsable(text).Should().BeTrue();
    }

    [Fact]
    public void Xlsx_reads_first_sheet_only()
    {
        var bytes = OfficeAiFillFixtures.TwoSheetXlsx(
            "NORTHRIDGE PLAT INDEX SHEET ONE",
            "SECRET SECOND SHEET MUST NOT APPEAR");
        using var stream = new MemoryStream(bytes);
        var text = OfficeTextExtractor.ExtractXlsxFirstSheet(stream);
        text.Should().Contain("NORTHRIDGE PLAT INDEX SHEET ONE");
        text.Should().NotContain("SECRET SECOND SHEET");
        OfficeTextExtractor.IsUsable(text).Should().BeTrue();
    }

    [Fact]
    public void Ole_wrapped_office_is_treated_as_password_or_legacy()
    {
        using var stream = new MemoryStream(OfficeAiFillFixtures.OleCompound);
        OfficeTextExtractor.LooksEncryptedOrLegacyBinary(stream).Should().BeTrue();
        var act = () => OfficeTextExtractor.ExtractDocx(stream);
        act.Should().Throw<InvalidDataException>().WithMessage(AiFillSourceKinds.PasswordMessage);
    }

    [Fact]
    public void Garbage_docx_fails_closed()
    {
        using var stream = new MemoryStream(OfficeAiFillFixtures.GarbageOffice("broken.docx"));
        var act = () => OfficeTextExtractor.ExtractDocx(stream);
        act.Should().Throw<InvalidDataException>().WithMessage("*could not be read*");
    }
}
