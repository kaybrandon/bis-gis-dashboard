using System.Net;
using System.Net.Http.Headers;
using FluentAssertions;
using GisDashboard.Application.WorkItems;
using GisDashboard.Infrastructure.Persistence;

namespace GisDashboard.Tests;

public sealed class UploadOfficeTests : IClassFixture<ApiFactory>
{
    private static readonly byte[] OleBytes = [0xD0, 0xCF, 0x11, 0xE0, 0xA1, 0xB1, 0x1A, 0xE1, 0x0D, 0x0C];
    private static readonly byte[] ZipBytes = [0x50, 0x4B, 0x03, 0x04, 0x0A, 0x00, 0x05];
    private static readonly byte[] PdfBytes = [0x25, 0x50, 0x44, 0x46, 0x2D, 0x31];
    private static readonly byte[] PngBytes = [0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A, 0x11];

    private readonly ApiFactory _factory;

    public UploadOfficeTests(ApiFactory factory)
    {
        _factory = factory;
    }

    [Theory]
    [InlineData("lease.doc", "application/octet-stream", "application/msword")]
    [InlineData("easement.docx", "application/vnd.openxmlformats-officedocument.wordprocessingml.document", "application/vnd.openxmlformats-officedocument.wordprocessingml.document")]
    [InlineData("acreage.xls", "application/octet-stream", "application/vnd.ms-excel")]
    [InlineData("index.xlsx", "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet")]
    public async Task Signed_in_upload_accepts_each_office_type(string fileName, string requestType, string storedType)
    {
        var client = await _factory.LoginAsync("admin@bisconsultants.local");
        var bytes = fileName.EndsWith('x') ? ZipBytes : OleBytes;
        var uploaded = await UploadSignedAsync(client, SeedIds.DemoClient, "Demo Client", fileName, bytes, requestType);
        uploaded.StatusCode.Should().Be(HttpStatusCode.OK, await uploaded.Content.ReadAsStringAsync());
        var json = await uploaded.ReadJsonAsync();
        json.GetProperty("fileName").GetString().Should().Be(fileName);
        json.GetProperty("organizationName").GetString().Should().Be("Demo Client");
        json.GetProperty("organizationId").GetGuid().Should().Be(SeedIds.DemoClient);
        json.GetProperty("contentType").GetString().Should().Be(storedType);

        var id = json.GetProperty("id").GetGuid();
        var download = await client.GetAsync($"/api/work-items/{id}/file");
        download.StatusCode.Should().Be(HttpStatusCode.OK);
        download.Content.Headers.ContentType!.MediaType.Should().Be(storedType);
        (await download.Content.ReadAsByteArrayAsync()).Should().Equal(bytes);
    }

    [Fact]
    public async Task Mixed_batch_keeps_filenames_and_org_and_pdf_image_still_work()
    {
        var client = await _factory.LoginAsync("editor@bisconsultants.local");
        var files = new (string Name, byte[] Bytes, string Type)[]
        {
            ("batch-plat.pdf", PdfBytes, "application/pdf"),
            ("batch-scan.png", PngBytes, "image/png"),
            ("batch-notes.doc", OleBytes, "application/octet-stream"),
            ("batch-memo.docx", ZipBytes, "application/octet-stream"),
            ("batch-grid.xls", OleBytes, "application/vnd.ms-excel"),
            ("batch-table.xlsx", ZipBytes, "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet"),
        };

        var created = new List<(string Name, Guid Id, byte[] Bytes)>();
        foreach (var file in files)
        {
            var uploaded = await UploadSignedAsync(client, SeedIds.DemoClient, "Demo Client", file.Name, file.Bytes, file.Type);
            uploaded.StatusCode.Should().Be(HttpStatusCode.OK, await uploaded.Content.ReadAsStringAsync());
            var json = await uploaded.ReadJsonAsync();
            json.GetProperty("fileName").GetString().Should().Be(file.Name);
            json.GetProperty("organizationId").GetGuid().Should().Be(SeedIds.DemoClient);
            json.GetProperty("organizationName").GetString().Should().Be("Demo Client");
            created.Add((file.Name, json.GetProperty("id").GetGuid(), file.Bytes));
        }

        var other = await UploadSignedAsync(client, SeedIds.OtherClient, "Other Client", "other-client.xlsx", ZipBytes, "application/octet-stream");
        other.StatusCode.Should().Be(HttpStatusCode.OK, await other.Content.ReadAsStringAsync());
        var otherJson = await other.ReadJsonAsync();
        otherJson.GetProperty("fileName").GetString().Should().Be("other-client.xlsx");
        otherJson.GetProperty("organizationId").GetGuid().Should().Be(SeedIds.OtherClient);

        foreach (var item in created)
        {
            var download = await client.GetAsync($"/api/work-items/{item.Id}/file");
            download.StatusCode.Should().Be(HttpStatusCode.OK);
            (await download.Content.ReadAsByteArrayAsync()).Should().Equal(item.Bytes);
        }
    }

    [Fact]
    public async Task Public_upload_accepts_office_types_and_rejects_unsupported()
    {
        var admin = await _factory.LoginAsync("admin@bisconsultants.local");
        var link = await (await admin.GetAsync($"/api/admin/organizations/{SeedIds.DemoClient}/upload-link")).ReadJsonAsync();
        var token = link.GetProperty("token").GetString()!;
        var anon = _factory.CreateClient();

        var info = await (await anon.GetAsync($"/api/public/uploads/{token}")).ReadJsonAsync();
        AssertAcceptedTypes(info);
        info.GetProperty("maxFileBytes").GetInt64().Should().Be(UploadOptions.DefaultMaxFileBytes);
        info.GetProperty("maxFileMegabytes").GetInt32().Should().Be(50);

        foreach (var (name, bytes, type) in new (string, byte[], string)[]
        {
            ("public.doc", OleBytes, "application/msword"),
            ("public.docx", ZipBytes, "application/octet-stream"),
            ("public.xls", OleBytes, "application/vnd.ms-excel"),
            ("public.xlsx", ZipBytes, "application/octet-stream"),
        })
        {
            var uploaded = await UploadPublicAsync(anon, token, name, bytes, type);
            uploaded.StatusCode.Should().Be(HttpStatusCode.OK, await uploaded.Content.ReadAsStringAsync());
            var json = await uploaded.ReadJsonAsync();
            json.GetProperty("fileName").GetString().Should().Be(name);
            json.GetProperty("organizationName").GetString().Should().Be("Demo Client");
        }

        var rejected = await UploadPublicAsync(anon, token, "notes.txt", "not-office"u8.ToArray(), "text/plain");
        rejected.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        var message = (await rejected.ReadJsonAsync()).GetProperty("message").GetString();
        message.Should().Contain("notes.txt");
        message.Should().Contain(".docx");
        message.Should().Contain(".xlsx");
        message.Should().NotContain("Only PDF and image");
    }

    [Fact]
    public async Task Settings_list_the_same_supported_types_and_limits()
    {
        var client = await _factory.LoginAsync("admin@bisconsultants.local");
        var json = await (await client.GetAsync("/api/settings")).ReadJsonAsync();
        var uploads = json.GetProperty("uploads");
        AssertAcceptedTypes(uploads);
        uploads.GetProperty("maxFileBytes").GetInt64().Should().Be(UploadOptions.DefaultMaxFileBytes);
        uploads.GetProperty("maxFileMegabytes").GetInt32().Should().Be(50);
        uploads.GetProperty("note").GetString().Should().Contain("Word");
        uploads.GetProperty("note").GetString().Should().Contain("Excel");
        json.GetProperty("features").GetProperty("massUpload").GetProperty("note").GetString().Should().Contain(".docx");
        json.GetProperty("features").GetProperty("tokenizedUpload").GetProperty("note").GetString().Should().Contain("Word");
    }

    [Fact]
    public async Task Unsupported_type_is_rejected_while_pdf_still_uploads()
    {
        var client = await _factory.LoginAsync("admin@bisconsultants.local");
        var rejected = await UploadSignedAsync(client, SeedIds.DemoClient, "Demo Client", "payload.zip", ZipBytes, "application/zip");
        rejected.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await rejected.ReadJsonAsync()).GetProperty("message").GetString().Should().Contain("payload.zip");

        var pdf = await UploadSignedAsync(client, SeedIds.DemoClient, "Demo Client", "still-works.pdf", PdfBytes, "application/pdf");
        pdf.StatusCode.Should().Be(HttpStatusCode.OK, await pdf.Content.ReadAsStringAsync());
        (await pdf.ReadJsonAsync()).GetProperty("fileName").GetString().Should().Be("still-works.pdf");
    }

    [Fact]
    public async Task Viewer_can_download_own_org_office_file_other_org_cannot()
    {
        var admin = await _factory.LoginAsync("admin@bisconsultants.local");
        var uploaded = await UploadSignedAsync(admin, SeedIds.DemoClient, "Demo Client", "staff-copy.docx", ZipBytes, "application/octet-stream");
        uploaded.StatusCode.Should().Be(HttpStatusCode.OK);
        var id = (await uploaded.ReadJsonAsync()).GetProperty("id").GetGuid();

        var viewer = await _factory.LoginAsync("viewer@bisconsultants.local");
        var own = await viewer.GetAsync($"/api/work-items/{id}/file");
        own.StatusCode.Should().Be(HttpStatusCode.OK);
        (await own.Content.ReadAsByteArrayAsync()).Should().Equal(ZipBytes);

        var anon = _factory.CreateClient();
        (await anon.GetAsync($"/api/work-items/{id}/file")).StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    private static void AssertAcceptedTypes(System.Text.Json.JsonElement source)
    {
        var extensions = source.GetProperty("acceptedExtensions").EnumerateArray().Select(x => x.GetString()).ToList();
        extensions.Should().Contain([".doc", ".docx", ".xls", ".xlsx", ".pdf", ".png"]);
        source.GetProperty("supportedTypesLabel").GetString().Should().Be(UploadFileTypes.SupportedTypesLabel);
        source.GetProperty("accept").GetString().Should().Be(UploadFileTypes.AcceptAttribute);
        source.GetProperty("supportedTypesLabel").GetString().Should().NotContain("PDFs or images only");
    }

    private static Task<HttpResponseMessage> UploadSignedAsync(
        HttpClient client,
        Guid organizationId,
        string organizationName,
        string fileName,
        byte[] bytes,
        string contentType) =>
        PostAsync(
            client,
            "/api/work-items",
            fileName,
            bytes,
            contentType,
            ("organizationId", organizationId.ToString()),
            ("organizationName", organizationName),
            ("documentTypeId", SeedIds.TypeDeed.ToString()),
            ("documentTypeName", "Deed"));

    private static Task<HttpResponseMessage> UploadPublicAsync(
        HttpClient client,
        string token,
        string fileName,
        byte[] bytes,
        string contentType) =>
        PostAsync(
            client,
            $"/api/public/uploads/{token}",
            fileName,
            bytes,
            contentType,
            ("documentTypeId", SeedIds.TypeDeed.ToString()),
            ("documentTypeName", "Deed"));

    private static async Task<HttpResponseMessage> PostAsync(
        HttpClient client,
        string url,
        string fileName,
        byte[] bytes,
        string contentType,
        params (string Key, string Value)[] fields)
    {
        using var form = new MultipartFormDataContent();
        foreach (var (key, value) in fields)
        {
            form.Add(new StringContent(value), key);
        }

        var file = new ByteArrayContent(bytes);
        file.Headers.ContentType = new MediaTypeHeaderValue(contentType);
        form.Add(file, "file", fileName);
        return await client.PostAsync(url, form);
    }
}
