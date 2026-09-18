using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using FluentAssertions;
using GisDashboard.Domain;
using GisDashboard.Infrastructure.Persistence;

namespace GisDashboard.Tests;

/// <summary>
/// GIS-UI-09 — soft-archive organizations. Same spirit as Users Archive.
/// </summary>
public sealed class OrganizationArchiveTests : IClassFixture<ApiFactory>
{
    private readonly ApiFactory _factory;

    public OrganizationArchiveTests(ApiFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public void Historical_name_keeps_identity_and_adds_archived_suffix()
    {
        OrganizationIdentity.HistoricalName("Demo Client", false).Should().Be("Demo Client");
        OrganizationIdentity.HistoricalName("Demo Client", true).Should().Be("Demo Client (archived)");
        OrganizationIdentity.HistoricalName("Demo Client (archived)", true).Should().Be("Demo Client (archived)");
    }

    [Fact]
    public async Task Archive_is_soft_delete_hides_from_default_lists_and_keeps_documents()
    {
        var admin = await _factory.LoginAsync("admin@bisconsultants.local");
        var created = await admin.PostAsJsonAsync("/api/admin/organizations", new
        {
            name = "Archive Soft Client",
            code = "archivesoft"
        });
        created.StatusCode.Should().Be(HttpStatusCode.OK, await created.Content.ReadAsStringAsync());
        var org = await created.ReadJsonAsync();
        var id = org.GetProperty("id").GetGuid();
        org.GetProperty("isArchived").GetBoolean().Should().BeFalse();

        var uploaded = await UploadAsync(admin, id.ToString(), "Archive Soft Client", "archive-history.pdf");
        uploaded.StatusCode.Should().Be(HttpStatusCode.OK, await uploaded.Content.ReadAsStringAsync());
        var workItem = await uploaded.ReadJsonAsync();
        var workItemId = workItem.GetProperty("id").GetGuid();

        var archived = await admin.PostAsync($"/api/admin/organizations/{id}/archive", null);
        archived.StatusCode.Should().Be(HttpStatusCode.OK, await archived.Content.ReadAsStringAsync());
        var archivedJson = await archived.ReadJsonAsync();
        archivedJson.GetProperty("isArchived").GetBoolean().Should().BeTrue();
        archivedJson.GetProperty("archivedAt").ValueKind.Should().NotBe(System.Text.Json.JsonValueKind.Null);

        var hidden = await (await admin.GetAsync("/api/admin/organizations")).ReadJsonAsync();
        hidden.EnumerateArray().Should().NotContain(x => x.GetProperty("id").GetGuid() == id);

        var shown = await (await admin.GetAsync("/api/admin/organizations?includeArchived=true")).ReadJsonAsync();
        shown.EnumerateArray().Should().Contain(x => x.GetProperty("id").GetGuid() == id);

        var lookups = await (await admin.GetAsync("/api/lookups/organizations")).ReadJsonAsync();
        lookups.EnumerateArray().Should().NotContain(x => x.GetProperty("id").GetGuid() == id);

        var archivedLookups = await (await admin.GetAsync("/api/lookups/organizations?includeArchived=true")).ReadJsonAsync();
        archivedLookups.EnumerateArray().Should().Contain(x =>
            x.GetProperty("id").GetGuid() == id && x.GetProperty("isArchived").GetBoolean());

        var me = await (await admin.GetAsync("/api/auth/me")).ReadJsonAsync();
        me.GetProperty("organizations").EnumerateArray()
            .Should().NotContain(x => x.GetProperty("id").GetGuid() == id);

        var detail = await (await admin.GetAsync($"/api/work-items/{workItemId}")).ReadJsonAsync();
        detail.GetProperty("organizationId").GetGuid().Should().Be(id);
        detail.GetProperty("organizationName").GetString().Should().Contain("Archive Soft Client");
        detail.GetProperty("organizationName").GetString().Should().Contain("archived");

        var list = await (await admin.GetAsync("/api/work-items?pageSize=100")).ReadJsonAsync();
        var row = list.GetProperty("items").EnumerateArray()
            .Single(x => x.GetProperty("id").GetGuid() == workItemId);
        row.GetProperty("organizationName").GetString().Should().Contain("Archive Soft Client");

        var blocked = await UploadAsync(admin, id.ToString(), "Archive Soft Client", "archive-blocked.pdf");
        blocked.StatusCode.Should().Be(HttpStatusCode.BadRequest);

        var deleteAttempt = await admin.DeleteAsync($"/api/admin/organizations/{id}");
        deleteAttempt.StatusCode.Should().BeOneOf(HttpStatusCode.MethodNotAllowed, HttpStatusCode.NotFound);

        var restored = await admin.PostAsync($"/api/admin/organizations/{id}/restore", null);
        restored.StatusCode.Should().Be(HttpStatusCode.OK, await restored.Content.ReadAsStringAsync());
        (await restored.ReadJsonAsync()).GetProperty("isArchived").GetBoolean().Should().BeFalse();

        var visible = await (await admin.GetAsync("/api/admin/organizations")).ReadJsonAsync();
        visible.EnumerateArray().Should().Contain(x => x.GetProperty("id").GetGuid() == id);
    }

    [Fact]
    public async Task Token_upload_and_editor_archive_are_blocked_for_archived_orgs()
    {
        var admin = await _factory.LoginAsync("admin@bisconsultants.local");
        var created = await admin.PostAsJsonAsync("/api/admin/organizations", new
        {
            name = "Archive Token Client",
            code = "archivetoken"
        });
        created.StatusCode.Should().Be(HttpStatusCode.OK, await created.Content.ReadAsStringAsync());
        var id = (await created.ReadJsonAsync()).GetProperty("id").GetGuid();

        var link = await (await admin.GetAsync($"/api/admin/organizations/{id}/upload-link")).ReadJsonAsync();
        var token = link.GetProperty("token").GetString();
        token.Should().NotBeNullOrWhiteSpace();

        (await admin.PostAsync($"/api/admin/organizations/{id}/archive", null)).StatusCode.Should().Be(HttpStatusCode.OK);

        var anon = _factory.CreateClient();
        var publicInfo = await anon.GetAsync($"/api/public/uploads/{token}");
        publicInfo.StatusCode.Should().Be(HttpStatusCode.NotFound);

        var editor = await _factory.LoginAsync("editor@bisconsultants.local");
        var editorArchive = await editor.PostAsync($"/api/admin/organizations/{id}/archive", null);
        editorArchive.StatusCode.Should().Be(HttpStatusCode.Forbidden);

        var editorRestore = await editor.PostAsync($"/api/admin/organizations/{id}/restore", null);
        editorRestore.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task Org_admin_can_archive_and_users_archive_is_unchanged()
    {
        var admin = await _factory.LoginAsync("admin@democlient.local");
        var created = await admin.PostAsJsonAsync("/api/admin/organizations", new
        {
            name = "Archive Org Admin Client",
            code = "archiveorgadmin"
        });
        // Org admin cannot create orgs — only Global Admin can.
        created.StatusCode.Should().Be(HttpStatusCode.Forbidden);

        var global = await _factory.LoginAsync("admin@bisconsultants.local");
        var org = await global.PostAsJsonAsync("/api/admin/organizations", new
        {
            name = "Archive Org Admin Client",
            code = "archiveorgadmin"
        });
        org.StatusCode.Should().Be(HttpStatusCode.OK, await org.Content.ReadAsStringAsync());
        var id = (await org.ReadJsonAsync()).GetProperty("id").GetGuid();

        var archived = await admin.PostAsync($"/api/admin/organizations/{id}/archive", null);
        archived.StatusCode.Should().Be(HttpStatusCode.OK, await archived.Content.ReadAsStringAsync());

        var userCreated = await admin.PostAsJsonAsync("/api/admin/users", new
        {
            email = "archive.org.stillworks@democlient.local",
            password = "Demo!Gis2026",
            displayName = "Org Archive User",
            role = "Viewer",
            organizationIds = new[] { SeedIds.DemoClient }
        });
        userCreated.StatusCode.Should().Be(HttpStatusCode.OK, await userCreated.Content.ReadAsStringAsync());
        var userId = (await userCreated.ReadJsonAsync()).GetProperty("id").GetGuid();
        (await admin.PostAsync($"/api/admin/users/{userId}/archive", null)).StatusCode.Should().Be(HttpStatusCode.OK);
        var hiddenUsers = await (await admin.GetAsync("/api/admin/users")).ReadJsonAsync();
        hiddenUsers.EnumerateArray().Should().NotContain(x => x.GetProperty("id").GetGuid() == userId);
    }

    [Fact]
    public async Task Cannot_assign_viewer_to_archived_organization()
    {
        var admin = await _factory.LoginAsync("admin@bisconsultants.local");
        var created = await admin.PostAsJsonAsync("/api/admin/organizations", new
        {
            name = "Archive Assign Client",
            code = "archiveassign"
        });
        created.StatusCode.Should().Be(HttpStatusCode.OK, await created.Content.ReadAsStringAsync());
        var id = (await created.ReadJsonAsync()).GetProperty("id").GetGuid();
        (await admin.PostAsync($"/api/admin/organizations/{id}/archive", null)).StatusCode.Should().Be(HttpStatusCode.OK);

        var user = await admin.PostAsJsonAsync("/api/admin/users", new
        {
            email = "archive.assign@democlient.local",
            password = "Demo!Gis2026",
            displayName = "Archive Assign",
            role = "Viewer",
            organizationIds = new[] { id }
        });
        user.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    private static async Task<HttpResponseMessage> UploadAsync(
        HttpClient client,
        string organizationId,
        string organizationName,
        string fileName)
    {
        using var form = new MultipartFormDataContent();
        form.Add(new StringContent(organizationId), "organizationId");
        form.Add(new StringContent(organizationName), "organizationName");
        form.Add(new StringContent(SeedIds.TypeDeed.ToString()), "documentTypeId");
        form.Add(new StringContent("Deed"), "documentTypeName");
        form.Add(new StringContent(Path.GetFileNameWithoutExtension(fileName)), "title");
        var file = new ByteArrayContent([0x25, 0x50, 0x44, 0x46, 0x2D]);
        file.Headers.ContentType = new MediaTypeHeaderValue("application/pdf");
        form.Add(file, "file", fileName);
        return await client.PostAsync("/api/work-items", form);
    }
}
