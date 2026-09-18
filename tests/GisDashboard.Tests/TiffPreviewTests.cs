using System.Net;
using System.Net.Http.Headers;
using FluentAssertions;
using GisDashboard.Application.WorkItems;
using GisDashboard.Infrastructure.Persistence;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;

namespace GisDashboard.Tests;

public sealed class TiffPreviewTests : IClassFixture<ApiFactory>
{
    private static readonly byte[] PngBytes = [0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A, 0x11];
    private static readonly byte[] OleBytes = [0xD0, 0xCF, 0x11, 0xE0, 0xA1, 0xB1, 0x1A, 0xE1, 0x0D, 0x0C];

    private readonly ApiFactory _factory;

    public TiffPreviewTests(ApiFactory factory)
    {
        _factory = factory;
    }

    [Theory]
    [InlineData("scan.tif")]
    [InlineData("scan.tiff")]
    public async Task Preview_opens_tiff_first_page_inline_instead_of_forcing_download(string fileName)
    {
        var client = await _factory.LoginAsync("admin@bisconsultants.local");
        await using var tiff = await TiffPreviewFixtures.SinglePageTiffAsync(Color.Red);
        var bytes = tiff.ToArray();
        var uploaded = await UploadSignedAsync(client, fileName, bytes, "application/octet-stream");
        uploaded.StatusCode.Should().Be(HttpStatusCode.OK, await uploaded.Content.ReadAsStringAsync());
        var id = (await uploaded.ReadJsonAsync()).GetProperty("id").GetGuid();
        (await uploaded.ReadJsonAsync()).GetProperty("contentType").GetString().Should().Be("image/tiff");

        var preview = await client.GetAsync($"/api/work-items/{id}/preview");
        preview.StatusCode.Should().Be(HttpStatusCode.OK, await preview.Content.ReadAsStringAsync());
        preview.Content.Headers.ContentType!.MediaType.Should().Be("image/png");
        preview.Content.Headers.ContentDisposition?.DispositionType.Should().NotBe("attachment");
        preview.Headers.GetValues("X-Preview-Page").Should().Equal("1");
        preview.Headers.GetValues("X-Preview-Page-Count").Should().Equal("1");
        preview.Headers.GetValues("X-Preview-Kind").Should().Equal(DocumentPreview.TiffKind);

        var png = await preview.Content.ReadAsByteArrayAsync();
        png.Take(4).Should().Equal(0x89, 0x50, 0x4E, 0x47);
        using var rendered = Image.Load<Rgba32>(png);
        rendered.Frames.Count.Should().Be(1);
        rendered[0, 0].R.Should().BeGreaterThan(200);

        var original = await client.GetAsync($"/api/work-items/{id}/file");
        original.StatusCode.Should().Be(HttpStatusCode.OK);
        original.Content.Headers.ContentType!.MediaType.Should().Be("image/tiff");
        (await original.Content.ReadAsByteArrayAsync()).Should().Equal(bytes);
    }

    [Fact]
    public async Task Multi_page_tiff_preview_is_first_page_and_reports_remaining_pages()
    {
        var client = await _factory.LoginAsync("editor@bisconsultants.local");
        await using var tiff = await TiffPreviewFixtures.MultiPageTiffAsync(Color.Red, Color.Blue);
        var uploaded = await UploadSignedAsync(client, "multipage.tiff", tiff.ToArray(), "image/tiff");
        uploaded.StatusCode.Should().Be(HttpStatusCode.OK, await uploaded.Content.ReadAsStringAsync());
        var id = (await uploaded.ReadJsonAsync()).GetProperty("id").GetGuid();

        var preview = await client.GetAsync($"/api/work-items/{id}/preview");
        preview.StatusCode.Should().Be(HttpStatusCode.OK);
        preview.Headers.GetValues("X-Preview-Page-Count").Should().Equal("2");
        preview.Headers.GetValues("X-Preview-More-Pages").Should().Equal("true");
        using var rendered = Image.Load<Rgba32>(await preview.Content.ReadAsByteArrayAsync());
        rendered.Frames.Count.Should().Be(1);
        rendered[0, 0].R.Should().BeGreaterThan(200);
        rendered[0, 0].B.Should().BeLessThan(50);
    }

    [Fact]
    public async Task Jpeg_preview_still_returns_the_original_image()
    {
        var client = await _factory.LoginAsync("admin@bisconsultants.local");
        var jpeg = await TiffPreviewFixtures.JpegBytesAsync();
        var uploaded = await UploadSignedAsync(client, "photo.jpg", jpeg, "image/jpeg");
        uploaded.StatusCode.Should().Be(HttpStatusCode.OK, await uploaded.Content.ReadAsStringAsync());
        var id = (await uploaded.ReadJsonAsync()).GetProperty("id").GetGuid();
        (await uploaded.ReadJsonAsync()).GetProperty("contentType").GetString().Should().Be("image/jpeg");

        var preview = await client.GetAsync($"/api/work-items/{id}/preview");
        preview.StatusCode.Should().Be(HttpStatusCode.OK);
        preview.Content.Headers.ContentType!.MediaType.Should().Be("image/jpeg");
        preview.Content.Headers.ContentDisposition?.DispositionType.Should().NotBe("attachment");
        preview.Headers.GetValues("X-Preview-Kind").Should().Equal(DocumentPreview.BrowserImageKind);
        (await preview.Content.ReadAsByteArrayAsync()).Should().Equal(jpeg);

        var file = await client.GetAsync($"/api/work-items/{id}/file");
        file.StatusCode.Should().Be(HttpStatusCode.OK);
        file.Content.Headers.ContentType!.MediaType.Should().Be("image/jpeg");
        (await file.Content.ReadAsByteArrayAsync()).Should().Equal(jpeg);

        var png = await UploadSignedAsync(client, "still-works.png", PngBytes, "image/png");
        png.StatusCode.Should().Be(HttpStatusCode.OK, await png.Content.ReadAsStringAsync());
        var pngId = (await png.ReadJsonAsync()).GetProperty("id").GetGuid();
        var pngPreview = await client.GetAsync($"/api/work-items/{pngId}/preview");
        pngPreview.StatusCode.Should().Be(HttpStatusCode.OK);
        (await pngPreview.Content.ReadAsByteArrayAsync()).Should().Equal(PngBytes);
    }

    [Fact]
    public async Task Preview_returns_clear_unavailable_message_for_office_and_bad_tiff()
    {
        var client = await _factory.LoginAsync("admin@bisconsultants.local");
        var office = await UploadSignedAsync(client, "lease.doc", OleBytes, "application/msword");
        office.StatusCode.Should().Be(HttpStatusCode.OK);
        var officeId = (await office.ReadJsonAsync()).GetProperty("id").GetGuid();
        var officePreview = await client.GetAsync($"/api/work-items/{officeId}/preview");
        officePreview.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await officePreview.ReadJsonAsync()).GetProperty("message").GetString()
            .Should().Be(DocumentPreview.UnavailableMessage);

        var bad = await UploadSignedAsync(client, "broken.tif", "not-a-tiff"u8.ToArray(), "image/tiff");
        bad.StatusCode.Should().Be(HttpStatusCode.OK);
        var badId = (await bad.ReadJsonAsync()).GetProperty("id").GetGuid();
        var badPreview = await client.GetAsync($"/api/work-items/{badId}/preview");
        badPreview.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await badPreview.ReadJsonAsync()).GetProperty("message").GetString()
            .Should().Be(DocumentPreview.TiffUnavailableMessage);
    }

    [Fact]
    public async Task Preview_is_org_scoped_and_requires_auth()
    {
        var admin = await _factory.LoginAsync("admin@bisconsultants.local");
        await using var tiff = await TiffPreviewFixtures.SinglePageTiffAsync(Color.Red);
        var uploaded = await UploadSignedAsync(admin, "idor.tif", tiff.ToArray(), "image/tiff");
        var id = (await uploaded.ReadJsonAsync()).GetProperty("id").GetGuid();

        var viewer = await _factory.LoginAsync("viewer@bisconsultants.local");
        (await viewer.GetAsync($"/api/work-items/{id}/preview")).StatusCode.Should().Be(HttpStatusCode.OK);

        var otherOrg = await PostAsync(
            admin,
            "/api/work-items",
            "other-org.tif",
            tiff.ToArray(),
            "image/tiff",
            ("organizationId", SeedIds.OtherClient.ToString()),
            ("organizationName", "Other Client"),
            ("documentTypeId", SeedIds.TypeDeed.ToString()),
            ("documentTypeName", "Deed"));
        otherOrg.StatusCode.Should().Be(HttpStatusCode.OK, await otherOrg.Content.ReadAsStringAsync());
        var otherOrgId = (await otherOrg.ReadJsonAsync()).GetProperty("id").GetGuid();
        (await viewer.GetAsync($"/api/work-items/{otherOrgId}/preview")).StatusCode.Should().Be(HttpStatusCode.NotFound);

        var anon = _factory.CreateClient();
        (await anon.GetAsync($"/api/work-items/{id}/preview")).StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    private static Task<HttpResponseMessage> UploadSignedAsync(
        HttpClient client,
        string fileName,
        byte[] bytes,
        string contentType) =>
        PostAsync(
            client,
            "/api/work-items",
            fileName,
            bytes,
            contentType,
            ("organizationId", SeedIds.DemoClient.ToString()),
            ("organizationName", "Demo Client"),
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
