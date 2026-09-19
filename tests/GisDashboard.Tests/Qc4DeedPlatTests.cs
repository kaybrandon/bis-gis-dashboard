using System.IO.Compression;
using System.Net;
using System.Net.Http.Json;
using System.Text;
using System.Xml.Linq;
using FluentAssertions;
using GisDashboard.Application.WorkItems;
using GisDashboard.Infrastructure.Export;
using GisDashboard.Infrastructure.Persistence;

namespace GisDashboard.Tests;

public sealed class Qc4DeedPlatTests : IClassFixture<ApiFactory>
{
    private readonly ApiFactory _factory;

    public Qc4DeedPlatTests(ApiFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task Editor_can_save_deed_plat_fields_and_export_them()
    {
        var client = await _factory.LoginAsync("editor@bisconsultants.local");
        var legal = "Lot 4, Block 2, \"Northridge\" Addition\nDallas County, TX";
        var patch = await client.PatchAsJsonAsync($"/api/work-items/{SeedIds.DemoHeld}", new
        {
            survey = "S-441",
            @abstract = "A-88, Tract 2",
            lotBlock = "Lot 4, Block 2",
            subdivision = "Northridge Addition",
            legalDescription = legal
        });
        patch.StatusCode.Should().Be(HttpStatusCode.OK);
        var json = await patch.ReadJsonAsync();
        json.GetProperty("survey").GetString().Should().Be("S-441");
        json.GetProperty("abstract").GetString().Should().Be("A-88, Tract 2");
        json.GetProperty("lotBlock").GetString().Should().Be("Lot 4, Block 2");
        json.GetProperty("subdivision").GetString().Should().Be("Northridge Addition");
        json.GetProperty("legalDescription").GetString().Should().Be(legal);
        json.GetProperty("deedPlatManual").GetProperty("survey").GetBoolean().Should().BeTrue();
        json.GetProperty("deedPlatManual").GetProperty("legalDescription").GetBoolean().Should().BeTrue();

        var csv = await client.GetAsync("/api/work-items/export?pageSize=100&format=csv");
        csv.StatusCode.Should().Be(HttpStatusCode.OK);
        csv.Content.Headers.ContentType!.MediaType.Should().Be("text/csv");
        var csvText = Encoding.UTF8.GetString(await csv.Content.ReadAsByteArrayAsync());
        csvText.Should().Contain("Survey,Abstract,Lot/Block,Subdivision,Legal Description");
        var held = FindCsvRow(csvText, "Boundary-question");
        held.Should().NotBeNull();
        held!["Survey"].Should().Be("S-441");
        held["Abstract"].Should().Be("A-88, Tract 2");
        held["Lot/Block"].Should().Be("Lot 4, Block 2");
        held["Subdivision"].Should().Be("Northridge Addition");
        held["Legal Description"].Should().Be(legal);

        var xlsx = await client.GetAsync("/api/work-items/export?pageSize=100&format=xlsx");
        xlsx.StatusCode.Should().Be(HttpStatusCode.OK);
        xlsx.Content.Headers.ContentType!.MediaType.Should().Be("application/vnd.openxmlformats-officedocument.spreadsheetml.sheet");
        var excelRow = FindExcelRow(await xlsx.Content.ReadAsByteArrayAsync(), "S-441");
        excelRow.Should().Contain("A-88, Tract 2");
        excelRow.Should().Contain("Lot 4, Block 2");
        excelRow.Should().Contain("Northridge Addition");
        excelRow.Should().Contain(legal);
    }

    [Fact]
    public async Task Uploader_cannot_edit_deed_plat_fields()
    {
        var client = await _factory.LoginAsync("uploader@bisconsultants.local");
        var patch = await client.PatchAsJsonAsync($"/api/work-items/{SeedIds.DemoPlat}", new
        {
            survey = "NOPE"
        });
        patch.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task Viewer_can_read_comments_but_not_internal_notes()
    {
        var viewer = await _factory.LoginAsync("viewer@bisconsultants.local");
        var detail = await (await viewer.GetAsync($"/api/work-items/{SeedIds.DemoPlat}")).ReadJsonAsync();
        detail.GetProperty("canPostComments").GetBoolean().Should().BeFalse();
        detail.GetProperty("canSeeInternalNotes").GetBoolean().Should().BeFalse();
        detail.GetProperty("internalNotes").ValueKind.Should().Be(System.Text.Json.JsonValueKind.Null);
        detail.GetProperty("canSeeTimeLogs").GetBoolean().Should().BeTrue();
        detail.GetProperty("canLogTime").GetBoolean().Should().BeFalse();

        var comments = await viewer.GetAsync($"/api/work-items/{SeedIds.DemoPlat}/comments");
        comments.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    private static Dictionary<string, string>? FindCsvRow(string text, string fileNameHint)
    {
        var rows = ParseCsv(text);
        return rows.FirstOrDefault(row => row.TryGetValue("FileName", out var name)
            && name.Contains(fileNameHint, StringComparison.OrdinalIgnoreCase));
    }

    private static List<Dictionary<string, string>> ParseCsv(string text)
    {
        var lines = SplitCsvRecords(text.TrimStart('\uFEFF'));
        if (lines.Count == 0)
        {
            return [];
        }

        var headers = lines[0];
        return lines.Skip(1).Select(values =>
        {
            var row = new Dictionary<string, string>(StringComparer.Ordinal);
            for (var i = 0; i < headers.Count; i++)
            {
                row[headers[i]] = i < values.Count ? values[i] : string.Empty;
            }

            return row;
        }).ToList();
    }

    private static List<List<string>> SplitCsvRecords(string text)
    {
        var records = new List<List<string>>();
        var row = new List<string>();
        var field = new StringBuilder();
        var quoted = false;
        for (var i = 0; i < text.Length; i++)
        {
            var ch = text[i];
            if (quoted)
            {
                if (ch == '"' && i + 1 < text.Length && text[i + 1] == '"')
                {
                    field.Append('"');
                    i++;
                }
                else if (ch == '"')
                {
                    quoted = false;
                }
                else
                {
                    field.Append(ch);
                }
            }
            else if (ch == '"')
            {
                quoted = true;
            }
            else if (ch == ',')
            {
                row.Add(field.ToString());
                field.Clear();
            }
            else if (ch == '\n')
            {
                if (field.Length > 0 || row.Count > 0)
                {
                    row.Add(field.ToString().TrimEnd('\r'));
                    records.Add(row);
                    row = [];
                    field.Clear();
                }
            }
            else
            {
                field.Append(ch);
            }
        }

        if (field.Length > 0 || row.Count > 0)
        {
            row.Add(field.ToString());
            records.Add(row);
        }

        return records;
    }

    private static string FindExcelRow(byte[] bytes, string marker)
    {
        using var zip = new ZipArchive(new MemoryStream(bytes), ZipArchiveMode.Read);
        using var stream = zip.GetEntry("xl/worksheets/sheet1.xml")!.Open();
        var xml = XDocument.Load(stream);
        XNamespace ns = "http://schemas.openxmlformats.org/spreadsheetml/2006/main";
        foreach (var row in xml.Descendants(ns + "row"))
        {
            var cells = row.Descendants(ns + "t").Select(x => x.Value).ToList();
            if (cells.Any(value => value.Contains(marker, StringComparison.Ordinal)))
            {
                return string.Join('\u001f', cells);
            }
        }

        return string.Empty;
    }
}

public sealed class Qc4DeedPlatAiTests : IClassFixture<AiFillConfiguredFactory>
{
    private readonly AiFillConfiguredFactory _factory;

    public Qc4DeedPlatAiTests(AiFillConfiguredFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task Ai_fill_populates_blank_deed_plat_fields_and_does_not_overwrite_saved_corrections()
    {
        ScriptedOpenAiCompletions.Reset();
        ScriptedOpenAiCompletions.ResponseJson = ScriptedOpenAiCompletions.JsonWithDeedPlat(
            "AI-SURVEY",
            "AI-ABSTRACT",
            "AI-LOT",
            "AI-SUB",
            "AI legal line 1\nAI legal line 2");
        var client = await _factory.LoginAsync("editor@bisconsultants.local");
        var uploaded = await AiFillUpload.PostAsync(client, AiFillUpload.MinimalTextPdf(), "deed-plat.pdf", "Deed plat");
        uploaded.StatusCode.Should().Be(HttpStatusCode.OK);
        var id = (await uploaded.ReadJsonAsync()).GetProperty("id").GetGuid();

        var fill = await client.PostAsync($"/api/work-items/{id}/ai-fill", null);
        fill.StatusCode.Should().Be(HttpStatusCode.OK);
        var filled = await (await client.GetAsync($"/api/work-items/{id}")).ReadJsonAsync();
        filled.GetProperty("survey").GetString().Should().Be("AI-SURVEY");
        filled.GetProperty("abstract").GetString().Should().Be("AI-ABSTRACT");
        filled.GetProperty("lotBlock").GetString().Should().Be("AI-LOT");
        filled.GetProperty("subdivision").GetString().Should().Be("AI-SUB");
        filled.GetProperty("legalDescription").GetString().Should().Be("AI legal line 1\nAI legal line 2");
        filled.GetProperty("deedPlatManual").GetProperty("survey").GetBoolean().Should().BeFalse();

        var patched = await client.PatchAsJsonAsync($"/api/work-items/{id}", new
        {
            survey = "STAFF-SURVEY",
            legalDescription = "Staff legal, with commas"
        });
        patched.StatusCode.Should().Be(HttpStatusCode.OK);

        ScriptedOpenAiCompletions.ResponseJson = ScriptedOpenAiCompletions.JsonWithDeedPlat(
            "NEW-SURVEY",
            "NEW-ABSTRACT",
            "NEW-LOT",
            "NEW-SUB",
            "NEW legal");
        var refill = await client.PostAsync($"/api/work-items/{id}/ai-fill", null);
        refill.StatusCode.Should().Be(HttpStatusCode.OK);
        var after = await (await client.GetAsync($"/api/work-items/{id}")).ReadJsonAsync();
        after.GetProperty("survey").GetString().Should().Be("STAFF-SURVEY");
        after.GetProperty("legalDescription").GetString().Should().Be("Staff legal, with commas");
        after.GetProperty("abstract").GetString().Should().Be("NEW-ABSTRACT");
        after.GetProperty("lotBlock").GetString().Should().Be("NEW-LOT");
        after.GetProperty("subdivision").GetString().Should().Be("NEW-SUB");
        after.GetProperty("deedPlatManual").GetProperty("survey").GetBoolean().Should().BeTrue();
        after.GetProperty("deedPlatManual").GetProperty("legalDescription").GetBoolean().Should().BeTrue();
        after.GetProperty("deedPlatManual").GetProperty("abstract").GetBoolean().Should().BeFalse();
        ScriptedOpenAiCompletions.LastSystemPrompt.Should().Contain("legalDescription");
        ScriptedOpenAiCompletions.LastSystemPrompt.Should().Contain("Do not extract or return propertyIds");
    }
}

public sealed class Qc4DeedPlatExportUnitTests
{
    [Fact]
    public void Csv_and_excel_keep_commas_quotes_and_line_breaks_in_legal_description()
    {
        var item = new WorkItemListItem(
            Guid.NewGuid(),
            "plat, \"quoted\".pdf",
            "Title",
            Guid.NewGuid(),
            "Client, Inc.",
            Guid.NewGuid(),
            "Plat",
            Guid.NewGuid(),
            "Active",
            "#1890ff",
            "Pat \"Editor\"",
            Guid.NewGuid(),
            null,
            DateTimeOffset.Parse("2026-09-19T12:00:00Z"),
            "Uploader",
            DateTimeOffset.Parse("2026-09-19T12:00:00Z"),
            DateTimeOffset.Parse("2026-09-18T00:00:00Z"),
            1.5m,
            "1h 30m",
            null,
            null,
            "application/pdf",
            12,
            false,
            null,
            false,
            null,
            "S-1",
            "A-2",
            "Lot 1, Block 2",
            "Oak Grove",
            "Tract A, \"Oak Grove\"\nDallas County");

        var csv = Encoding.UTF8.GetString(WorkItemCsvExport.Build([item], "text/csv").Content);
        csv.Should().Contain("Survey");
        csv.Should().Contain("Legal Description");
        csv.Should().Contain("\"plat, \"\"quoted\"\".pdf\"");
        csv.Should().Contain("\"Tract A, \"\"Oak Grove\"\"\nDallas County\"");

        var excel = WorkItemExcelExport.Build([item]);
        excel.FileName.Should().Be("gis-work-items.xlsx");
        using var zip = new ZipArchive(new MemoryStream(excel.Content), ZipArchiveMode.Read);
        using var stream = zip.GetEntry("xl/worksheets/sheet1.xml")!.Open();
        var xml = XDocument.Load(stream);
        XNamespace ns = "http://schemas.openxmlformats.org/spreadsheetml/2006/main";
        var cells = xml.Descendants(ns + "t").Select(x => x.Value).ToList();
        cells.Should().Contain("Survey");
        cells.Should().Contain("Lot/Block");
        cells.Should().Contain("Legal Description");
        cells.Should().Contain("plat, \"quoted\".pdf");
        cells.Should().Contain("Tract A, \"Oak Grove\"\nDallas County");
        cells.Should().Contain("Lot 1, Block 2");
    }
}
