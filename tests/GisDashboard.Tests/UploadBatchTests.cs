using System.Net;
using System.Net.Http.Headers;
using FluentAssertions;
using GisDashboard.Application.WorkItems;
using GisDashboard.Infrastructure.Persistence;

namespace GisDashboard.Tests;

public sealed class UploadBatchTests : IClassFixture<ApiFactory>
{
    private readonly ApiFactory _factory;

    public UploadBatchTests(ApiFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task Settings_expose_default_50mb_limit_and_concurrency_3()
    {
        var client = await _factory.LoginAsync("admin@bisconsultants.local");
        var json = await (await client.GetAsync("/api/settings")).ReadJsonAsync();
        var uploads = json.GetProperty("uploads");
        uploads.GetProperty("maxFileBytes").GetInt64().Should().Be(UploadOptions.DefaultMaxFileBytes);
        uploads.GetProperty("maxFileMegabytes").GetInt32().Should().Be(50);
        uploads.GetProperty("concurrency").GetInt32().Should().Be(3);
        json.GetProperty("features").GetProperty("massUpload").GetProperty("enabled").GetBoolean().Should().BeTrue();
    }

    [Fact]
    public async Task Two_signed_in_uploads_each_become_their_own_work_item()
    {
        var client = await _factory.LoginAsync("admin@bisconsultants.local");
        var first = await UploadSignedAsync(client, "batch-north.pdf", SeedIds.TypeDeed, "Deed", isPriority: true, note: "Needed Friday");
        var second = await UploadSignedAsync(client, "batch-south.pdf", SeedIds.TypePlat, "Plat", isPriority: true, note: "Needed Friday");
        first.StatusCode.Should().Be(HttpStatusCode.OK, await first.Content.ReadAsStringAsync());
        second.StatusCode.Should().Be(HttpStatusCode.OK, await second.Content.ReadAsStringAsync());

        var a = await first.ReadJsonAsync();
        var b = await second.ReadJsonAsync();
        a.GetProperty("id").GetGuid().Should().NotBe(b.GetProperty("id").GetGuid());
        a.GetProperty("fileName").GetString().Should().Be("batch-north.pdf");
        b.GetProperty("fileName").GetString().Should().Be("batch-south.pdf");
        a.GetProperty("documentTypeName").GetString().Should().Be("Deed");
        b.GetProperty("documentTypeName").GetString().Should().Be("Plat");
        a.GetProperty("organizationId").GetGuid().Should().Be(SeedIds.DemoClient);
        b.GetProperty("organizationId").GetGuid().Should().Be(SeedIds.DemoClient);
        a.GetProperty("isPriority").GetBoolean().Should().BeTrue();
        b.GetProperty("isPriority").GetBoolean().Should().BeTrue();
        a.GetProperty("priorityNote").GetString().Should().Be("Needed Friday");
        b.GetProperty("priorityNote").GetString().Should().Be("Needed Friday");
        a.GetProperty("statusName").GetString().Should().Be("Pending");
        b.GetProperty("statusName").GetString().Should().Be("Pending");
    }

    [Fact]
    public async Task Public_upload_info_includes_size_limit_and_two_token_uploads_succeed()
    {
        var admin = await _factory.LoginAsync("admin@bisconsultants.local");
        var link = await (await admin.GetAsync($"/api/admin/organizations/{SeedIds.DemoClient}/upload-link")).ReadJsonAsync();
        var token = link.GetProperty("token").GetString()!;

        var anon = _factory.CreateClient();
        var info = await (await anon.GetAsync($"/api/public/uploads/{token}")).ReadJsonAsync();
        info.GetProperty("organizationName").GetString().Should().Be("Demo Client");
        info.GetProperty("maxFileBytes").GetInt64().Should().Be(UploadOptions.DefaultMaxFileBytes);
        info.GetProperty("maxFileMegabytes").GetInt32().Should().Be(50);
        info.GetProperty("concurrency").GetInt32().Should().Be(3);
        info.TryGetProperty("organizationId", out _).Should().BeFalse();

        var first = await UploadPublicAsync(anon, token, "public-one.pdf");
        var second = await UploadPublicAsync(anon, token, "public-two.pdf");
        first.StatusCode.Should().Be(HttpStatusCode.OK, await first.Content.ReadAsStringAsync());
        second.StatusCode.Should().Be(HttpStatusCode.OK, await second.Content.ReadAsStringAsync());
        var a = await first.ReadJsonAsync();
        var b = await second.ReadJsonAsync();
        a.GetProperty("id").GetGuid().Should().NotBe(b.GetProperty("id").GetGuid());
        a.GetProperty("fileName").GetString().Should().Be("public-one.pdf");
        b.GetProperty("fileName").GetString().Should().Be("public-two.pdf");
        a.GetProperty("organizationName").GetString().Should().Be("Demo Client");
    }

    private static Task<HttpResponseMessage> UploadSignedAsync(
        HttpClient client,
        string fileName,
        Guid documentTypeId,
        string documentTypeName,
        bool isPriority = false,
        string? note = null,
        byte[]? bytes = null) =>
        PostMultipartAsync(
            client,
            "/api/work-items",
            fileName,
            bytes,
            ("organizationId", SeedIds.DemoClient.ToString()),
            ("organizationName", "Demo Client"),
            ("documentTypeId", documentTypeId.ToString()),
            ("documentTypeName", documentTypeName),
            ("isPriority", isPriority ? "true" : "false"),
            ("priorityNote", note ?? ""));

    private static Task<HttpResponseMessage> UploadPublicAsync(HttpClient client, string token, string fileName, byte[]? bytes = null) =>
        PostMultipartAsync(
            client,
            $"/api/public/uploads/{token}",
            fileName,
            bytes,
            ("documentTypeId", SeedIds.TypeDeed.ToString()),
            ("documentTypeName", "Deed"));

    internal static async Task<HttpResponseMessage> PostMultipartAsync(
        HttpClient client,
        string url,
        string fileName,
        byte[]? bytes,
        params (string Key, string Value)[] fields)
    {
        using var form = new MultipartFormDataContent();
        foreach (var (key, value) in fields)
        {
            if (!string.IsNullOrEmpty(value))
            {
                form.Add(new StringContent(value), key);
            }
        }

        var payload = bytes ?? [0x25, 0x50, 0x44, 0x46, 0x2D];
        var file = new ByteArrayContent(payload);
        file.Headers.ContentType = new MediaTypeHeaderValue("application/pdf");
        form.Add(file, "file", fileName);
        return await client.PostAsync(url, form);
    }
}

public sealed class TightUploadApiFactory : ApiFactory
{
    protected override void ExtraConfig(Dictionary<string, string?> config)
    {
        config["Uploads:MaxFileBytes"] = "200";
        config["Uploads:Concurrency"] = "3";
    }
}

public sealed class UploadSizeLimitTests : IClassFixture<TightUploadApiFactory>
{
    private readonly TightUploadApiFactory _factory;

    public UploadSizeLimitTests(TightUploadApiFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task Oversized_signed_in_file_is_rejected_and_the_next_file_still_uploads()
    {
        var client = await _factory.LoginAsync("admin@bisconsultants.local");
        var settings = await (await client.GetAsync("/api/settings")).ReadJsonAsync();
        settings.GetProperty("uploads").GetProperty("maxFileBytes").GetInt64().Should().Be(200);

        var tooBig = await UploadBatchTests.PostMultipartAsync(
            client,
            "/api/work-items",
            "too-big.pdf",
            Enumerable.Repeat((byte)0x41, 300).ToArray(),
            ("organizationId", SeedIds.DemoClient.ToString()),
            ("organizationName", "Demo Client"),
            ("documentTypeId", SeedIds.TypeDeed.ToString()),
            ("documentTypeName", "Deed"));
        tooBig.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        var message = (await tooBig.ReadJsonAsync()).GetProperty("message").GetString();
        message.Should().Contain("too-big.pdf");
        message.Should().Contain("per-file limit");
        message.Should().Contain("not uploaded");

        var ok = await UploadBatchTests.PostMultipartAsync(
            client,
            "/api/work-items",
            "small-enough.pdf",
            [0x25, 0x50, 0x44, 0x46, 0x2D],
            ("organizationId", SeedIds.DemoClient.ToString()),
            ("organizationName", "Demo Client"),
            ("documentTypeId", SeedIds.TypeDeed.ToString()),
            ("documentTypeName", "Deed"));
        ok.StatusCode.Should().Be(HttpStatusCode.OK, await ok.Content.ReadAsStringAsync());
        (await ok.ReadJsonAsync()).GetProperty("fileName").GetString().Should().Be("small-enough.pdf");
    }

    [Fact]
    public async Task Oversized_public_file_is_rejected_and_the_next_file_still_uploads()
    {
        var admin = await _factory.LoginAsync("admin@bisconsultants.local");
        var link = await (await admin.GetAsync($"/api/admin/organizations/{SeedIds.DemoClient}/upload-link")).ReadJsonAsync();
        var token = link.GetProperty("token").GetString()!;
        var anon = _factory.CreateClient();

        var info = await (await anon.GetAsync($"/api/public/uploads/{token}")).ReadJsonAsync();
        info.GetProperty("maxFileBytes").GetInt64().Should().Be(200);

        var tooBig = await UploadBatchTests.PostMultipartAsync(
            anon,
            $"/api/public/uploads/{token}",
            "huge-token.pdf",
            Enumerable.Repeat((byte)0x42, 250).ToArray(),
            ("documentTypeId", SeedIds.TypeDeed.ToString()),
            ("documentTypeName", "Deed"));
        tooBig.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await tooBig.ReadJsonAsync()).GetProperty("message").GetString().Should().Contain("per-file limit");

        var ok = await UploadBatchTests.PostMultipartAsync(
            anon,
            $"/api/public/uploads/{token}",
            "ok-token.pdf",
            [0x25, 0x50, 0x44, 0x46, 0x2D],
            ("documentTypeId", SeedIds.TypeDeed.ToString()),
            ("documentTypeName", "Deed"));
        ok.StatusCode.Should().Be(HttpStatusCode.OK, await ok.Content.ReadAsStringAsync());
        (await ok.ReadJsonAsync()).GetProperty("fileName").GetString().Should().Be("ok-token.pdf");
    }
}
