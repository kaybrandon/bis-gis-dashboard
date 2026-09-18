using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using GisDashboard.Infrastructure.Persistence;

namespace GisDashboard.Tests;

public sealed class IdorAndRoleTests : IClassFixture<ApiFactory>
{
    private readonly ApiFactory _factory;

    public IdorAndRoleTests(ApiFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task Unauthenticated_requests_are_unauthorized()
    {
        var client = _factory.CreateClient();
        var response = await client.GetAsync("/api/work-items");
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Editor_can_read_other_org_work_item()
    {
        var client = await _factory.LoginAsync("editor@bisconsultants.local");
        var response = await client.GetAsync($"/api/work-items/{SeedIds.OtherPlat}");
        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task Org_admin_can_read_other_org_work_item()
    {
        var client = await _factory.LoginAsync("admin@democlient.local");
        var response = await client.GetAsync($"/api/work-items/{SeedIds.OtherPlat}");
        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task Org_admin_list_includes_every_organization()
    {
        var client = await _factory.LoginAsync("admin@democlient.local");
        var response = await client.GetAsync("/api/work-items?pageSize=100");
        var json = await response.ReadJsonAsync();
        response.IsSuccessStatusCode.Should().BeTrue(json.ToString());
        json.GetProperty("items").EnumerateArray()
            .Select(x => x.GetProperty("organizationName").GetString())
            .Should().Contain("Demo Client")
            .And.Contain("Other Client");
    }

    [Fact]
    public async Task Viewer_list_never_includes_other_org()
    {
        var client = await _factory.LoginAsync("viewer@bisconsultants.local");
        var response = await client.GetAsync("/api/work-items?pageSize=100");
        var json = await response.ReadJsonAsync();
        response.IsSuccessStatusCode.Should().BeTrue(json.ToString());
        json.GetProperty("items").EnumerateArray()
            .Select(x => x.GetProperty("organizationName").GetString())
            .Should().OnlyContain(name => name == "Demo Client");
    }

    [Fact]
    public async Task Global_admin_can_read_both_orgs()
    {
        var client = await _factory.LoginAsync("admin@bisconsultants.local");
        (await client.GetAsync($"/api/work-items/{SeedIds.DemoPlat}")).StatusCode.Should().Be(HttpStatusCode.OK);
        (await client.GetAsync($"/api/work-items/{SeedIds.OtherPlat}")).StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task Org_admin_can_read_internal_notes_on_own_org()
    {
        var client = await _factory.LoginAsync("admin@democlient.local");
        var json = await (await client.GetAsync($"/api/work-items/{SeedIds.DemoPlat}")).ReadJsonAsync();
        json.GetProperty("canSeeInternalNotes").GetBoolean().Should().BeTrue();
        json.GetProperty("canEditInternalNotes").GetBoolean().Should().BeTrue();
        json.GetProperty("internalNotes").GetString().Should().NotBeNullOrWhiteSpace();
    }

    [Fact]
    public async Task Viewer_cannot_see_or_edit_internal_notes()
    {
        var client = await _factory.LoginAsync("viewer@bisconsultants.local");
        var json = await (await client.GetAsync($"/api/work-items/{SeedIds.DemoPlat}")).ReadJsonAsync();
        json.GetProperty("canSeeInternalNotes").GetBoolean().Should().BeFalse();
        json.GetProperty("internalNotes").ValueKind.Should().Be(System.Text.Json.JsonValueKind.Null);

        var patch = await client.PatchAsJsonAsync($"/api/work-items/{SeedIds.DemoPlat}", new { internalNotes = "viewer rewrite" });
        patch.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task Viewer_cannot_upload()
    {
        var client = await _factory.LoginAsync("viewer@bisconsultants.local");
        using var form = new MultipartFormDataContent();
        form.Add(new StringContent(SeedIds.DemoClient.ToString()), "organizationId");
        form.Add(new StringContent(SeedIds.TypePlat.ToString()), "documentTypeId");
        form.Add(new ByteArrayContent([0x25, 0x50, 0x44, 0x46] ) { Headers = { ContentType = new("application/pdf") } }, "file", "x.pdf");
        var response = await client.PostAsync("/api/work-items", form);
        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task Viewer_cannot_change_status_or_assignment()
    {
        var client = await _factory.LoginAsync("viewer@bisconsultants.local");
        var response = await client.PatchAsJsonAsync($"/api/work-items/{SeedIds.DemoPlat}", new
        {
            statusId = SeedIds.StatusWorked,
            assignedToUserId = SeedIds.EditorDemo
        });
        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task Editor_cannot_list_users()
    {
        var editor = await _factory.LoginAsync("editor@bisconsultants.local");
        (await editor.GetAsync("/api/admin/users")).StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task Editor_can_update_internal_notes_on_assigned_org()
    {
        var client = await _factory.LoginAsync("editor@bisconsultants.local");
        var response = await client.PatchAsJsonAsync($"/api/work-items/{SeedIds.DemoSurvey}", new
        {
            internalNotes = "Shared note from editor"
        });
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var json = await response.ReadJsonAsync();
        json.GetProperty("internalNotes").GetString().Should().Be("Shared note from editor");
    }

    [Fact]
    public async Task Login_accepts_email_or_username()
    {
        var client = _factory.CreateClient();
        var emailLogin = await client.PostAsJsonAsync("/api/auth/login", new
        {
            email = "admin@bisconsultants.local",
            password = _factory.DemoPassword
        });
        emailLogin.StatusCode.Should().Be(HttpStatusCode.OK);
        var json = await emailLogin.ReadJsonAsync();
        json.GetProperty("user").GetProperty("role").GetString().Should().Be("GlobalAdministrator");
        json.GetProperty("user").GetProperty("roleDisplayName").GetString().Should().Be("Global Administrator");
        json.GetProperty("user").GetProperty("email").GetString().Should().Be("admin@bisconsultants.local");
        json.GetProperty("user").GetProperty("userName").GetString().Should().Be("admin");
        json.GetProperty("user").GetProperty("canManageGlobalDirectory").GetBoolean().Should().BeTrue();

        var userLogin = await client.PostAsJsonAsync("/api/auth/login", new
        {
            email = "ARivera",
            password = _factory.DemoPassword
        });
        userLogin.StatusCode.Should().Be(HttpStatusCode.OK);
        var editor = await userLogin.ReadJsonAsync();
        editor.GetProperty("user").GetProperty("email").GetString().Should().Be("editor@bisconsultants.local");
        editor.GetProperty("user").GetProperty("userName").GetString().Should().Be("arivera");
        editor.GetProperty("user").GetProperty("fullName").GetString().Should().Be("Alex Rivera");
        editor.GetProperty("user").GetProperty("displayName").GetString().Should().Be("arivera");
    }
}
