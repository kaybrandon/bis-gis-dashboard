using System.IO.Compression;
using System.Text;
using System.Xml.Linq;
using GisDashboard.Application.WorkItems;

namespace GisDashboard.Infrastructure.Export;

public static class WorkItemExcelExport
{
    private static readonly XNamespace Main = "http://schemas.openxmlformats.org/spreadsheetml/2006/main";
    private static readonly XNamespace Rels = "http://schemas.openxmlformats.org/officeDocument/2006/relationships";

    public static ExcelExport Build(IReadOnlyList<WorkItemListItem> items)
    {
        using var stream = new MemoryStream();
        using (var zip = new ZipArchive(stream, ZipArchiveMode.Create, leaveOpen: true))
        {
            Write(zip, "[Content_Types].xml", ContentTypes());
            Write(zip, "_rels/.rels", PackageRels());
            Write(zip, "xl/workbook.xml", Workbook());
            Write(zip, "xl/_rels/workbook.xml.rels", WorkbookRels());
            Write(zip, "xl/worksheets/sheet1.xml", Sheet(items));
        }

        return new ExcelExport(
            stream.ToArray(),
            "gis-work-items.xlsx",
            "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet");
    }

    private static string ContentTypes() =>
        """
        <?xml version="1.0" encoding="UTF-8" standalone="yes"?>
        <Types xmlns="http://schemas.openxmlformats.org/package/2006/content-types">
          <Default Extension="rels" ContentType="application/vnd.openxmlformats-package.relationships+xml"/>
          <Default Extension="xml" ContentType="application/xml"/>
          <Override PartName="/xl/workbook.xml" ContentType="application/vnd.openxmlformats-officedocument.spreadsheetml.sheet.main+xml"/>
          <Override PartName="/xl/worksheets/sheet1.xml" ContentType="application/vnd.openxmlformats-officedocument.spreadsheetml.worksheet+xml"/>
        </Types>
        """;

    private static string PackageRels() =>
        """
        <?xml version="1.0" encoding="UTF-8" standalone="yes"?>
        <Relationships xmlns="http://schemas.openxmlformats.org/package/2006/relationships">
          <Relationship Id="rId1" Type="http://schemas.openxmlformats.org/officeDocument/2006/relationships/officeDocument" Target="xl/workbook.xml"/>
        </Relationships>
        """;

    private static string Workbook() =>
        """
        <?xml version="1.0" encoding="UTF-8" standalone="yes"?>
        <workbook xmlns="http://schemas.openxmlformats.org/spreadsheetml/2006/main" xmlns:r="http://schemas.openxmlformats.org/officeDocument/2006/relationships">
          <sheets>
            <sheet name="Work items" sheetId="1" r:id="rId1"/>
          </sheets>
        </workbook>
        """;

    private static string WorkbookRels() =>
        """
        <?xml version="1.0" encoding="UTF-8" standalone="yes"?>
        <Relationships xmlns="http://schemas.openxmlformats.org/package/2006/relationships">
          <Relationship Id="rId1" Type="http://schemas.openxmlformats.org/officeDocument/2006/relationships/worksheet" Target="worksheets/sheet1.xml"/>
        </Relationships>
        """;

    private static string Sheet(IReadOnlyList<WorkItemListItem> items)
    {
        var rows = new List<XElement> { Row(1, WorkItemExportColumns.Headers) };
        var index = 2;
        foreach (var item in items)
        {
            rows.Add(Row(index, WorkItemExportColumns.Values(item)));
            index++;
        }

        var sheet = new XDocument(
            new XDeclaration("1.0", "UTF-8", "yes"),
            new XElement(Main + "worksheet",
                new XAttribute(XNamespace.Xmlns + "r", Rels),
                new XElement(Main + "sheetData", rows)));
        return sheet.Declaration + Environment.NewLine + sheet;
    }

    private static XElement Row(int rowIndex, IReadOnlyList<string> values)
    {
        var cells = new List<XElement>(values.Count);
        for (var i = 0; i < values.Count; i++)
        {
            cells.Add(new XElement(Main + "c",
                new XAttribute("r", CellRef(i, rowIndex)),
                new XAttribute("t", "inlineStr"),
                new XElement(Main + "is",
                    new XElement(Main + "t",
                        new XAttribute(XNamespace.Xml + "space", "preserve"),
                        Sanitize(values[i])))));
        }

        return new XElement(Main + "row", new XAttribute("r", rowIndex), cells);
    }

    private static string CellRef(int columnIndex, int rowIndex)
    {
        var name = string.Empty;
        var n = columnIndex;
        do
        {
            name = (char)('A' + (n % 26)) + name;
            n = (n / 26) - 1;
        } while (n >= 0);

        return name + rowIndex;
    }

    private static string Sanitize(string value)
    {
        if (string.IsNullOrEmpty(value))
        {
            return string.Empty;
        }

        var builder = new StringBuilder(value.Length);
        foreach (var ch in value)
        {
            if (ch is '\t' or '\n' or '\r' || !char.IsControl(ch))
            {
                builder.Append(ch);
            }
        }

        return builder.ToString();
    }

    private static void Write(ZipArchive zip, string path, string content)
    {
        var entry = zip.CreateEntry(path, CompressionLevel.Fastest);
        using var writer = new StreamWriter(entry.Open(), new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));
        writer.Write(content);
    }
}
