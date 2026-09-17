using System.Net;
using System.Net.Http.Headers;
using FluentAssertions;
using GisDashboard.Infrastructure.Persistence;

namespace GisDashboard.Tests;

public sealed class UploadOrgTests : IClassFixture<ApiFactory>
{
    private readonly ApiFactory _factory;

    public UploadOrgTests(ApiFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task Global_admin_can_upload_to_other_client_by_id()
    {
        var client = await _factory.LoginAsync("admin@bisconsultants.local");
        var response = await UploadAsync(client, SeedIds.OtherClient.ToString(), "Other Client", SeedIds.TypeDeed.ToString(), "Deed", "Split 17217.pdf");
        response.StatusCode.Should().Be(HttpStatusCode.OK, await response.Content.ReadAsStringAsync());
        var json = await response.ReadJsonAsync();
        json.GetProperty("organizationName").GetString().Should().Be("Other Client");
        json.GetProperty("documentTypeName").GetString().Should().Be("Deed");
        json.GetProperty("fileName").GetString().Should().Be("Split 17217.pdf");
        json.GetProperty("title").GetString().Should().Be("Split 17217");
    }

    [Fact]
    public async Task Global_admin_can_upload_when_org_id_is_missing_but_name_matches_dropdown()
    {
        var client = await _factory.LoginAsync("admin@bisconsultants.local");
        var response = await UploadAsync(client, "", "Other Client", SeedIds.TypeDeed.ToString(), "Deed", "Split 17217.pdf");
        response.StatusCode.Should().Be(HttpStatusCode.OK, await response.Content.ReadAsStringAsync());
        (await response.ReadJsonAsync()).GetProperty("organizationName").GetString().Should().Be("Other Client");
    }

    [Fact]
    public async Task Global_admin_can_upload_when_org_id_is_stale_seed_but_name_matches()
    {
        var client = await _factory.LoginAsync("admin@bisconsultants.local");
        var staleId = Guid.Parse("aaaaaaaa-ffff-0000-0000-000000000002");
        var response = await UploadAsync(client, staleId.ToString(), "Other Client", "", "Deed", "Split 17217.pdf");
        response.StatusCode.Should().Be(HttpStatusCode.OK, await response.Content.ReadAsStringAsync());
        var json = await response.ReadJsonAsync();
        json.GetProperty("organizationName").GetString().Should().Be("Other Client");
        json.GetProperty("organizationId").GetGuid().Should().Be(SeedIds.OtherClient);
        json.GetProperty("documentTypeName").GetString().Should().Be("Deed");
    }

    [Fact]
    public async Task Lookups_include_every_org_global_admin_can_upload_to()
    {
        var client = await _factory.LoginAsync("admin@bisconsultants.local");
        var lookups = await (await client.GetAsync("/api/lookups/organizations")).ReadJsonAsync();
        var names = lookups.EnumerateArray().Select(x => x.GetProperty("name").GetString()).ToList();
        names.Should().Contain("Other Client");
        names.Should().Contain("Demo Client");

        foreach (var org in lookups.EnumerateArray())
        {
            var response = await UploadAsync(
                client,
                org.GetProperty("id").GetGuid().ToString(),
                org.GetProperty("name").GetString()!,
                SeedIds.TypeDeed.ToString(),
                "Deed",
                $"{org.GetProperty("code").GetString()}.pdf");
            response.StatusCode.Should().Be(HttpStatusCode.OK, await response.Content.ReadAsStringAsync());
        }
    }

    [Fact]
    public async Task Org_admin_can_upload_to_other_client()
    {
        var client = await _factory.LoginAsync("admin@democlient.local");
        var response = await UploadAsync(client, SeedIds.OtherClient.ToString(), "Other Client", SeedIds.TypeDeed.ToString(), "Deed", "staff-cross.pdf");
        response.StatusCode.Should().Be(HttpStatusCode.OK, await response.Content.ReadAsStringAsync());
        (await response.ReadJsonAsync()).GetProperty("organizationName").GetString().Should().Be("Other Client");
    }

    [Fact]
    public async Task Viewer_cannot_upload_to_other_client()
    {
        var client = await _factory.LoginAsync("viewer@bisconsultants.local");
        var response = await UploadAsync(client, SeedIds.OtherClient.ToString(), "Other Client", SeedIds.TypeDeed.ToString(), "Deed", "nope.pdf");
        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
        (await response.ReadJsonAsync()).GetProperty("message").GetString().Should().Be("Organization was not found.");
    }

    [Fact]
    public async Task Editor_can_upload_to_assigned_client()
    {
        var client = await _factory.LoginAsync("editor@bisconsultants.local");
        var response = await UploadAsync(client, SeedIds.DemoClient.ToString(), "Demo Client", SeedIds.TypeDeed.ToString(), "Deed", "editor-upload.pdf");
        response.StatusCode.Should().Be(HttpStatusCode.OK, await response.Content.ReadAsStringAsync());
        (await response.ReadJsonAsync()).GetProperty("organizationName").GetString().Should().Be("Demo Client");
    }

    [Fact]
    public async Task Upload_without_organization_returns_validation_error()
    {
        var client = await _factory.LoginAsync("admin@bisconsultants.local");
        var response = await UploadAsync(client, "", "", SeedIds.TypeDeed.ToString(), "Deed", "missing-org.pdf");
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await response.ReadJsonAsync()).GetProperty("message").GetString().Should().Be("An organization is required.");
    }

    private static async Task<HttpResponseMessage> UploadAsync(
        HttpClient client,
        string organizationId,
        string organizationName,
        string documentTypeId,
        string documentTypeName,
        string fileName)
    {
        using var form = new MultipartFormDataContent();
        if (!string.IsNullOrEmpty(organizationId))
        {
            form.Add(new StringContent(organizationId), "organizationId");
        }

        if (!string.IsNullOrEmpty(organizationName))
        {
            form.Add(new StringContent(organizationName), "organizationName");
        }

        if (!string.IsNullOrEmpty(documentTypeId))
        {
            form.Add(new StringContent(documentTypeId), "documentTypeId");
        }

        if (!string.IsNullOrEmpty(documentTypeName))
        {
            form.Add(new StringContent(documentTypeName), "documentTypeName");
        }

        form.Add(new StringContent(Path.GetFileNameWithoutExtension(fileName)), "title");
        var file = new ByteArrayContent([0x25, 0x50, 0x44, 0x46, 0x2D]);
        file.Headers.ContentType = new MediaTypeHeaderValue("application/pdf");
        form.Add(file, "file", fileName);
        return await client.PostAsync("/api/work-items", form);
    }
}
