using System.Net;
using System.Net.Http.Headers;
using FluentAssertions;
using GisDashboard.Infrastructure.Persistence;

namespace GisDashboard.Tests;

public sealed class UploadNotesTests : IClassFixture<ApiFactory>
{
    private readonly ApiFactory _factory;

    public UploadNotesTests(ApiFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task Signed_in_upload_writes_optional_client_notes_as_a_comment()
    {
        var viewer = await _factory.LoginAsync("viewer@bisconsultants.local");
        var uploaded = await PostSignedAsync(viewer, "notes-from-client.pdf", "Please check the west line.");
        uploaded.StatusCode.Should().Be(HttpStatusCode.OK, await uploaded.Content.ReadAsStringAsync());
        var id = (await uploaded.ReadJsonAsync()).GetProperty("id").GetGuid();

        var comments = await (await viewer.GetAsync($"/api/work-items/{id}/comments")).ReadJsonAsync();
        comments.EnumerateArray().Select(x => x.GetProperty("body").GetString())
            .Should().Contain("Please check the west line.");
    }

    [Fact]
    public async Task Signed_in_upload_without_notes_has_no_comment()
    {
        var viewer = await _factory.LoginAsync("viewer@bisconsultants.local");
        var uploaded = await PostSignedAsync(viewer, "no-notes.pdf");
        uploaded.StatusCode.Should().Be(HttpStatusCode.OK, await uploaded.Content.ReadAsStringAsync());
        var id = (await uploaded.ReadJsonAsync()).GetProperty("id").GetGuid();
        var comments = await (await viewer.GetAsync($"/api/work-items/{id}/comments")).ReadJsonAsync();
        comments.GetArrayLength().Should().Be(0);
    }

    [Fact]
    public async Task Public_upload_info_shows_primary_tech_as_first_name_and_last_initial()
    {
        var admin = await _factory.LoginAsync("admin@bisconsultants.local");
        var link = await (await admin.GetAsync($"/api/admin/organizations/{SeedIds.DemoClient}/upload-link")).ReadJsonAsync();
        var token = link.GetProperty("token").GetString()!;

        var anon = _factory.CreateClient();
        var info = await (await anon.GetAsync($"/api/public/uploads/{token}")).ReadJsonAsync();
        var names = info.GetProperty("assignedTechnicians").EnumerateArray()
            .Select(x => x.GetProperty("name").GetString())
            .ToList();
        names.Should().ContainSingle().Which.Should().Be("Alex R.");
        names.Should().NotContain(n => n != null && n.Contains("Rivera", StringComparison.OrdinalIgnoreCase));

        var uploaded = await PostPublicAsync(anon, token!, "token-notes.pdf", "Need this for Friday filing.");
        uploaded.StatusCode.Should().Be(HttpStatusCode.OK, await uploaded.Content.ReadAsStringAsync());
        var id = (await uploaded.ReadJsonAsync()).GetProperty("id").GetGuid();

        var comments = await (await admin.GetAsync($"/api/work-items/{id}/comments")).ReadJsonAsync();
        comments.EnumerateArray().Select(x => x.GetProperty("body").GetString())
            .Should().Contain("Need this for Friday filing.");
    }

    [Fact]
    public async Task Signed_in_assigned_technicians_are_org_scoped_and_short_names()
    {
        var viewer = await _factory.LoginAsync("viewer@bisconsultants.local");
        var demo = await (await viewer.GetAsync($"/api/lookups/assigned-technicians?organizationId={SeedIds.DemoClient}")).ReadJsonAsync();
        demo.EnumerateArray().Select(x => x.GetProperty("name").GetString()).Should().Contain("Alex R.");

        var other = await viewer.GetAsync($"/api/lookups/assigned-technicians?organizationId={SeedIds.OtherClient}");
        other.StatusCode.Should().Be(HttpStatusCode.NotFound);

        var editor = await _factory.LoginAsync("editor@bisconsultants.local");
        var otherTechs = await (await editor.GetAsync($"/api/lookups/assigned-technicians?organizationId={SeedIds.OtherClient}")).ReadJsonAsync();
        otherTechs.EnumerateArray().Select(x => x.GetProperty("name").GetString())
            .Should().Contain("Casey N.")
            .And.NotContain(n => n != null && n.Contains("Nguyen", StringComparison.OrdinalIgnoreCase));
    }

    private static async Task<HttpResponseMessage> PostSignedAsync(HttpClient client, string fileName, string? notes = null)
    {
        using var form = new MultipartFormDataContent();
        form.Add(new StringContent(SeedIds.DemoClient.ToString()), "organizationId");
        form.Add(new StringContent("Demo Client"), "organizationName");
        form.Add(new StringContent(Path.GetFileNameWithoutExtension(fileName)), "title");
        if (!string.IsNullOrWhiteSpace(notes))
        {
            form.Add(new StringContent(notes), "clientNotes");
        }

        var file = new ByteArrayContent([0x25, 0x50, 0x44, 0x46, 0x2D]);
        file.Headers.ContentType = new MediaTypeHeaderValue("application/pdf");
        form.Add(file, "file", fileName);
        return await client.PostAsync("/api/work-items", form);
    }

    private static async Task<HttpResponseMessage> PostPublicAsync(HttpClient client, string token, string fileName, string notes)
    {
        using var form = new MultipartFormDataContent();
        form.Add(new StringContent(SeedIds.TypeDeed.ToString()), "documentTypeId");
        form.Add(new StringContent("Deed"), "documentTypeName");
        form.Add(new StringContent(notes), "clientNotes");
        var file = new ByteArrayContent([0x25, 0x50, 0x44, 0x46, 0x2D]);
        file.Headers.ContentType = new MediaTypeHeaderValue("application/pdf");
        form.Add(file, "file", fileName);
        return await client.PostAsync($"/api/public/uploads/{token}", form);
    }
}
