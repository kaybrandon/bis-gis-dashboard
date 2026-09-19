using System.IO.Compression;
using System.Text;
using DocumentFormat.OpenXml;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Spreadsheet;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;
using W = DocumentFormat.OpenXml.Wordprocessing;

namespace GisDashboard.Tests;

internal static class OfficeAiFillFixtures
{
    public static readonly byte[] OleCompound =
        [0xD0, 0xCF, 0x11, 0xE0, 0xA1, 0xB1, 0x1A, 0xE1, 0x00, 0x00];

    public static byte[] TextDocx(string text)
    {
        using var stream = new MemoryStream();
        using (var document = WordprocessingDocument.Create(stream, WordprocessingDocumentType.Document, true))
        {
            var main = document.AddMainDocumentPart();
            main.Document = new W.Document(
                new W.Body(
                    new W.Paragraph(
                        new W.Run(
                            new W.Text(text)))));
            main.Document.Save();
        }

        return stream.ToArray();
    }

    public static byte[] TwoSheetXlsx(string firstSheet, string secondSheet)
    {
        using var stream = new MemoryStream();
        using (var spreadsheet = SpreadsheetDocument.Create(stream, SpreadsheetDocumentType.Workbook, true))
        {
            var workbook = spreadsheet.AddWorkbookPart();
            workbook.Workbook = new Workbook();
            var sheets = workbook.Workbook.AppendChild(new Sheets());
            AddInlineSheet(workbook, sheets, "Index", 1, "rId1", firstSheet);
            AddInlineSheet(workbook, sheets, "Other", 2, "rId2", secondSheet);
            workbook.Workbook.Save();
        }

        return stream.ToArray();
    }

    public static byte[] EmptyDocx() => TextDocx("   ");

    public static byte[] GarbageOffice(string _) => "not-an-office-file"u8.ToArray();

    public static byte[] EmptyZipAsDocx()
    {
        using var stream = new MemoryStream();
        using (var zip = new ZipArchive(stream, ZipArchiveMode.Create, true, Encoding.UTF8))
        {
            zip.CreateEntry("readme.txt").Open().Dispose();
        }

        return stream.ToArray();
    }

    public static async Task<byte[]> PngBytesAsync()
    {
        using var image = new Image<Rgba32>(16, 16, SixLabors.ImageSharp.Color.SteelBlue);
        using var stream = new MemoryStream();
        await image.SaveAsPngAsync(stream);
        return stream.ToArray();
    }

    public static async Task<byte[]> JpegBytesAsync()
    {
        using var image = new Image<Rgba32>(16, 16, SixLabors.ImageSharp.Color.Orange);
        using var stream = new MemoryStream();
        await image.SaveAsJpegAsync(stream);
        return stream.ToArray();
    }

    private static void AddInlineSheet(
        WorkbookPart workbook,
        Sheets sheets,
        string name,
        uint sheetId,
        string relationshipId,
        string cellText)
    {
        var part = workbook.AddNewPart<WorksheetPart>(relationshipId);
        var cell = new Cell
        {
            CellReference = "A1",
            DataType = CellValues.InlineString,
            InlineString = new InlineString(new DocumentFormat.OpenXml.Spreadsheet.Text(cellText))
        };
        part.Worksheet = new Worksheet(new SheetData(new Row(cell)));
        sheets.Append(new Sheet
        {
            Name = name,
            SheetId = sheetId,
            Id = relationshipId
        });
    }
}
