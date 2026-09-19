using System.Text;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Spreadsheet;
using DocumentFormat.OpenXml.Wordprocessing;
using GisDashboard.Application.AiFill;

namespace GisDashboard.Infrastructure.AiFill;

/// <summary>
/// Extracts text from DOCX and from the first XLSX sheet. Does not open DOC/XLS
/// or execute macros. Password-protected / encrypted packages fail closed.
/// </summary>
public static class OfficeTextExtractor
{
    private static readonly byte[] OleSignature = [0xD0, 0xCF, 0x11, 0xE0, 0xA1, 0xB1, 0x1A, 0xE1];
    private static readonly byte[] ZipSignature = [0x50, 0x4B];

    public static string ExtractDocx(Stream stream, int maxPages = PdfTextExtractor.DefaultMaxPages, int maxChars = PdfTextExtractor.DefaultMaxChars)
    {
        ArgumentNullException.ThrowIfNull(stream);
        EnsureOpenXmlPackage(stream, "DOCX");

        using var document = WordprocessingDocument.Open(stream, false);
        var body = document.MainDocumentPart?.Document?.Body;
        if (body is null)
        {
            return string.Empty;
        }

        var builder = new StringBuilder();
        var pages = 1;
        foreach (var node in body.Descendants())
        {
            if (node is DocumentFormat.OpenXml.Wordprocessing.Text text && !string.IsNullOrEmpty(text.Text))
            {
                builder.Append(text.Text);
                if (builder.Length >= maxChars)
                {
                    break;
                }
            }
            else if (node is DocumentFormat.OpenXml.Wordprocessing.Break br
                && br.Type?.Value == BreakValues.Page)
            {
                pages++;
                builder.AppendLine();
                if (pages > Math.Max(1, maxPages))
                {
                    break;
                }
            }
            else if (node is Paragraph)
            {
                if (builder.Length > 0 && builder[^1] != '\n')
                {
                    builder.AppendLine();
                }
            }

            if (builder.Length >= maxChars)
            {
                break;
            }
        }

        return Clip(builder.ToString(), maxChars);
    }

    public static string ExtractXlsxFirstSheet(Stream stream, int maxChars = PdfTextExtractor.DefaultMaxChars)
    {
        ArgumentNullException.ThrowIfNull(stream);
        EnsureOpenXmlPackage(stream, "XLSX");

        using var spreadsheet = SpreadsheetDocument.Open(stream, false);
        var workbook = spreadsheet.WorkbookPart ?? throw new InvalidDataException(AiFillSourceKinds.UnreadableMessage);
        var sheet = workbook.Workbook?.Sheets?.Elements<Sheet>().FirstOrDefault()
            ?? throw new InvalidDataException(AiFillSourceKinds.EmptyOfficeMessage);
        if (string.IsNullOrWhiteSpace(sheet.Id?.Value))
        {
            return string.Empty;
        }

        if (workbook.GetPartById(sheet.Id.Value) is not WorksheetPart worksheet)
        {
            return string.Empty;
        }

        var shared = workbook.SharedStringTablePart?.SharedStringTable;
        var builder = new StringBuilder();
        foreach (var row in worksheet.Worksheet.Descendants<Row>())
        {
            var cells = new List<string>();
            foreach (var cell in row.Elements<Cell>())
            {
                var value = ReadCell(cell, shared);
                if (!string.IsNullOrWhiteSpace(value))
                {
                    cells.Add(value);
                }
            }

            if (cells.Count == 0)
            {
                continue;
            }

            if (builder.Length > 0)
            {
                builder.AppendLine();
            }

            builder.Append(string.Join('\t', cells));
            if (builder.Length >= maxChars)
            {
                break;
            }
        }

        return Clip(builder.ToString(), maxChars);
    }

    public static bool IsUsable(string? text)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return false;
        }

        var letters = text.Count(char.IsLetter);
        var digits = text.Count(char.IsDigit);
        return letters >= 8 || letters + digits >= 12;
    }

    public static bool LooksEncryptedOrLegacyBinary(Stream stream)
    {
        ArgumentNullException.ThrowIfNull(stream);
        var position = stream.CanSeek ? stream.Position : 0;
        Span<byte> header = stackalloc byte[8];
        var read = stream.Read(header);
        if (stream.CanSeek)
        {
            stream.Position = position;
        }

        if (read >= OleSignature.Length && header[..OleSignature.Length].SequenceEqual(OleSignature))
        {
            return true;
        }

        return false;
    }

    private static void EnsureOpenXmlPackage(Stream stream, string kind)
    {
        if (LooksEncryptedOrLegacyBinary(stream))
        {
            throw new InvalidDataException(AiFillSourceKinds.PasswordMessage);
        }

        if (!LooksZip(stream))
        {
            throw new InvalidDataException(
                $"This {kind} could not be read. Replace it with a supported file, or type the fields.");
        }
    }

    private static bool LooksZip(Stream stream)
    {
        var position = stream.CanSeek ? stream.Position : 0;
        Span<byte> header = stackalloc byte[2];
        var read = stream.Read(header);
        if (stream.CanSeek)
        {
            stream.Position = position;
        }

        return read >= ZipSignature.Length && header[..ZipSignature.Length].SequenceEqual(ZipSignature);
    }

    private static string ReadCell(Cell cell, SharedStringTable? shared)
    {
        var raw = cell.InnerText;
        if (cell.DataType?.Value == CellValues.SharedString
            && int.TryParse(cell.CellValue?.Text, out var index)
            && shared is not null
            && index >= 0
            && index < shared.Count())
        {
            return shared.ElementAt(index).InnerText.Trim();
        }

        if (cell.DataType?.Value == CellValues.InlineString)
        {
            return cell.InlineString?.InnerText?.Trim() ?? string.Empty;
        }

        if (!string.IsNullOrWhiteSpace(cell.CellValue?.Text))
        {
            return cell.CellValue.Text.Trim();
        }

        return raw.Trim();
    }

    private static string Clip(string text, int maxChars)
    {
        var extracted = text.Trim();
        return extracted.Length <= maxChars ? extracted : extracted[..maxChars];
    }
}
