using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using GisDashboard.Domain;
using GisDashboard.Infrastructure.Persistence;

namespace GisDashboard.Tests;

/// <summary>
/// GIS Users — last successful login, Title, and Archive soft-delete.
/// GIS Users page only (not Admin Dashboard).
/// </summary>
public sealed class UsersLastActivityTests : IClassFixture<ApiFactory>
{
    private readonly ApiFactory _factory;

    public UsersLastActivityTests(ApiFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public void Historical_name_keeps_identity_and_adds_archived_suffix()
    {
        UserIdentity.HistoricalName("Alex Rivera", "arivera", false).Should().Be("Alex Rivera");
        UserIdentity.HistoricalName("Alex Rivera", "arivera", true).Should().Be("Alex Rivera (archived)");
        UserIdentity.WithArchivedSuffix("Alex Rivera (archived)", true).Should().Be("Alex Rivera (archived)");
        UserIdentity.CanSignIn(true, false).Should().BeTrue();
        UserIdentity.CanSignIn(true, true).Should().BeFalse();
        UserIdentity.CanSignIn(false, false).Should().BeFalse();
    }

    [Fact]
    public async Task Successful_login_writes_last_login_at_never_logged_in_stays_null()
    {
        var admin = await _factory.LoginAsync("admin@bisconsultants.local");
        var created = await admin.PostAsJsonAsync("/api/admin/users", new
        {
            email = "last.activity.never@democlient.local",
            password = "Demo!Gis2026",
            displayName = "Quiet Client",
            title = "Surveyor",
            role = "Viewer",
            organizationIds = new[] { SeedIds.DemoClient }
        });
        created.StatusCode.Should().Be(HttpStatusCode.OK, await created.Content.ReadAsStringAsync());
        var createdJson = await created.ReadJsonAsync();
        createdJson.GetProperty("lastLoginAt").ValueKind.Should().Be(System.Text.Json.JsonValueKind.Null);
        createdJson.GetProperty("title").GetString().Should().Be("Surveyor");
        createdJson.GetProperty("isArchived").GetBoolean().Should().BeFalse();
        var id = createdJson.GetProperty("id").GetGuid();

        var listed = await (await admin.GetAsync("/api/admin/users")).ReadJsonAsync();
        var quiet = listed.EnumerateArray().Single(x => x.GetProperty("id").GetGuid() == id);
        quiet.GetProperty("lastLoginAt").ValueKind.Should().Be(System.Text.Json.JsonValueKind.Null);
        quiet.GetProperty("title").GetString().Should().Be("Surveyor");

        var before = DateTimeOffset.UtcNow.AddSeconds(-5);
        var signedIn = await _factory.LoginAsync("last.activity.never@democlient.local");
        (await signedIn.GetAsync("/api/auth/me")).StatusCode.Should().Be(HttpStatusCode.OK);

        var afterLogin = await (await admin.GetAsync("/api/admin/users")).ReadJsonAsync();
        var active = afterLogin.EnumerateArray().Single(x => x.GetProperty("id").GetGuid() == id);
        var stamp = active.GetProperty("lastLoginAt").GetDateTimeOffset();
        stamp.Should().BeOnOrAfter(before);
        stamp.Should().BeOnOrBefore(DateTimeOffset.UtcNow.AddSeconds(5));
    }

    [Fact]
    public async Task Archive_is_soft_delete_hides_from_default_list_and_blocks_login()
    {
        var admin = await _factory.LoginAsync("admin@bisconsultants.local");
        var created = await admin.PostAsJsonAsync("/api/admin/users", new
        {
            email = "archive.login@democlient.local",
            password = "Demo!Gis2026",
            displayName = "Archive Login",
            role = "Editor",
            organizationIds = Array.Empty<Guid>()
        });
        created.StatusCode.Should().Be(HttpStatusCode.OK, await created.Content.ReadAsStringAsync());
        var id = (await created.ReadJsonAsync()).GetProperty("id").GetGuid();

        var archived = await admin.PostAsync($"/api/admin/users/{id}/archive", null);
        archived.StatusCode.Should().Be(HttpStatusCode.OK, await archived.Content.ReadAsStringAsync());
        var archivedJson = await archived.ReadJsonAsync();
        archivedJson.GetProperty("isArchived").GetBoolean().Should().BeTrue();
        archivedJson.GetProperty("archivedAt").ValueKind.Should().NotBe(System.Text.Json.JsonValueKind.Null);

        var hidden = await (await admin.GetAsync("/api/admin/users")).ReadJsonAsync();
        hidden.EnumerateArray().Should().NotContain(x => x.GetProperty("id").GetGuid() == id);

        var shown = await (await admin.GetAsync("/api/admin/users?includeArchived=true")).ReadJsonAsync();
        shown.EnumerateArray().Should().Contain(x => x.GetProperty("id").GetGuid() == id);

        var login = await _factory.CreateClient().PostAsJsonAsync("/api/auth/login", new
        {
            email = "archive.login@democlient.local",
            password = "Demo!Gis2026"
        });
        login.StatusCode.Should().Be(HttpStatusCode.Forbidden);

        var restored = await admin.PostAsync($"/api/admin/users/{id}/restore", null);
        restored.StatusCode.Should().Be(HttpStatusCode.OK, await restored.Content.ReadAsStringAsync());
        (await restored.ReadJsonAsync()).GetProperty("isArchived").GetBoolean().Should().BeFalse();

        var again = await _factory.CreateClient().PostAsJsonAsync("/api/auth/login", new
        {
            email = "archive.login@democlient.local",
            password = "Demo!Gis2026"
        });
        again.StatusCode.Should().Be(HttpStatusCode.OK, await again.Content.ReadAsStringAsync());
    }

    [Fact]
    public async Task Cannot_archive_self()
    {
        var admin = await _factory.LoginAsync("admin@bisconsultants.local");
        var response = await admin.PostAsync($"/api/admin/users/{SeedIds.Admin}/archive", null);
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Archived_assignee_stays_on_document_and_cannot_be_newly_assigned()
    {
        var admin = await _factory.LoginAsync("admin@bisconsultants.local");
        var created = await admin.PostAsJsonAsync("/api/admin/users", new
        {
            email = "archive.assignee@bisconsultants.local",
            password = "Demo!Gis2026",
            displayName = "Pat Assignee",
            fullName = "Pat Assignee",
            role = "Editor",
            organizationIds = Array.Empty<Guid>()
        });
        created.StatusCode.Should().Be(HttpStatusCode.OK, await created.Content.ReadAsStringAsync());
        var id = (await created.ReadJsonAsync()).GetProperty("id").GetGuid();

        var assigned = await admin.PatchAsJsonAsync($"/api/work-items/{SeedIds.DemoPlat}", new
        {
            assignedToUserId = id
        });
        assigned.StatusCode.Should().Be(HttpStatusCode.OK, await assigned.Content.ReadAsStringAsync());

        (await admin.PostAsync($"/api/admin/users/{id}/archive", null)).StatusCode.Should().Be(HttpStatusCode.OK);

        var detail = await (await admin.GetAsync($"/api/work-items/{SeedIds.DemoPlat}")).ReadJsonAsync();
        detail.GetProperty("assignedToUserId").GetGuid().Should().Be(id);
        detail.GetProperty("assignedToName").GetString().Should().Contain("Pat Assignee");
        detail.GetProperty("assignedToName").GetString().Should().Contain("archived");

        var list = await (await admin.GetAsync("/api/work-items?pageSize=100")).ReadJsonAsync();
        var row = list.GetProperty("items").EnumerateArray()
            .Single(x => x.GetProperty("id").GetGuid() == SeedIds.DemoPlat);
        row.GetProperty("assignedToName").GetString().Should().Contain("Pat Assignee");
        row.GetProperty("assignedToName").GetString().Should().Contain("archived");

        var pickers = await (await admin.GetAsync($"/api/lookups/assignable-users?organizationId={SeedIds.DemoClient}")).ReadJsonAsync();
        pickers.EnumerateArray().Should().NotContain(x => x.GetProperty("id").GetGuid() == id);

        var scoped = await (await admin.GetAsync("/api/lookups/assignees")).ReadJsonAsync();
        scoped.EnumerateArray().Should().NotContain(x => x.GetProperty("id").GetGuid() == id);

        var reassign = await admin.PatchAsJsonAsync($"/api/work-items/{SeedIds.DemoSurvey}", new
        {
            assignedToUserId = id
        });
        reassign.StatusCode.Should().Be(HttpStatusCode.BadRequest);

        await admin.PatchAsJsonAsync($"/api/work-items/{SeedIds.DemoPlat}", new
        {
            assignedToUserId = SeedIds.EditorDemo
        });
        await admin.PostAsync($"/api/admin/users/{id}/restore", null);
    }

    [Fact]
    public async Task Org_admin_user_list_visibility_is_unchanged_and_hides_archived()
    {
        var admin = await _factory.LoginAsync("admin@democlient.local");
        var created = await admin.PostAsJsonAsync("/api/admin/users", new
        {
            email = "archive.orgscope@democlient.local",
            password = "Demo!Gis2026",
            displayName = "Org Scope",
            role = "Viewer",
            organizationIds = new[] { SeedIds.DemoClient }
        });
        created.StatusCode.Should().Be(HttpStatusCode.OK, await created.Content.ReadAsStringAsync());
        var id = (await created.ReadJsonAsync()).GetProperty("id").GetGuid();

        (await admin.PostAsync($"/api/admin/users/{id}/archive", null)).StatusCode.Should().Be(HttpStatusCode.OK);

        var hidden = await (await admin.GetAsync("/api/admin/users")).ReadJsonAsync();
        hidden.EnumerateArray().Select(x => x.GetProperty("email").GetString())
            .Should().Contain("editor@bisconsultants.local")
            .And.NotContain("admin@bisconsultants.local")
            .And.NotContain("archive.orgscope@democlient.local");

        var shown = await (await admin.GetAsync("/api/admin/users?includeArchived=true")).ReadJsonAsync();
        shown.EnumerateArray().Select(x => x.GetProperty("email").GetString())
            .Should().Contain("archive.orgscope@democlient.local")
            .And.NotContain("admin@bisconsultants.local");
    }

    [Fact]
    public async Task Title_round_trips_on_create_and_update()
    {
        var admin = await _factory.LoginAsync("admin@bisconsultants.local");
        var created = await admin.PostAsJsonAsync("/api/admin/users", new
        {
            email = "title.roundtrip@democlient.local",
            password = "Demo!Gis2026",
            displayName = "Title Person",
            fullName = "Title Person",
            title = "GIS Analyst",
            role = "Viewer",
            organizationIds = new[] { SeedIds.DemoClient }
        });
        created.StatusCode.Should().Be(HttpStatusCode.OK, await created.Content.ReadAsStringAsync());
        var json = await created.ReadJsonAsync();
        var id = json.GetProperty("id").GetGuid();
        json.GetProperty("title").GetString().Should().Be("GIS Analyst");

        var updated = await admin.PutAsJsonAsync($"/api/admin/users/{id}", new
        {
            displayName = "Title Person",
            fullName = "Title Person",
            title = "County Surveyor",
            email = "title.roundtrip@democlient.local",
            role = "Viewer",
            organizationIds = new[] { SeedIds.DemoClient },
            isActive = true
        });
        updated.StatusCode.Should().Be(HttpStatusCode.OK, await updated.Content.ReadAsStringAsync());
        (await updated.ReadJsonAsync()).GetProperty("title").GetString().Should().Be("County Surveyor");
    }
}
