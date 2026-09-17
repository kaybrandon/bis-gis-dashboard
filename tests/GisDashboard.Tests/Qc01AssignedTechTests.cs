using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using FluentAssertions;
using GisDashboard.Infrastructure.Persistence;

namespace GisDashboard.Tests;

/// <summary>
/// QC01 — Admin and Editor can reassign organization Assigned tech(s).
/// Viewer (upload-capable client) cannot. Multi-tech is preserved.
/// Changing org techs does not force-reassign existing work-item Assigned To.
/// </summary>
public sealed class Qc01AssignedTechTests : IClassFixture<ApiFactory>
{
    private readonly ApiFactory _factory;

    public Qc01AssignedTechTests(ApiFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task Auth_flags_admin_and_editor_can_manage_assigned_techs_viewer_cannot()
    {
        foreach (var email in new[] { "admin@bisconsultants.local", "admin@democlient.local", "editor@bisconsultants.local" })
        {
            var client = await _factory.LoginAsync(email);
            var me = await (await client.GetAsync("/api/auth/me")).ReadJsonAsync();
            me.GetProperty("canManageAssignedTechs").GetBoolean().Should().BeTrue(email);
        }

        var viewer = await _factory.LoginAsync("viewer@bisconsultants.local");
        var viewerMe = await (await viewer.GetAsync("/api/auth/me")).ReadJsonAsync();
        viewerMe.GetProperty("canManageAssignedTechs").GetBoolean().Should().BeFalse();
        viewerMe.GetProperty("canUpload").GetBoolean().Should().BeTrue();
    }

    [Fact]
    public async Task Admin_can_save_assigned_techs_and_grid_persists_on_reopen()
    {
        var client = await _factory.LoginAsync("admin@democlient.local");
        await AssertReplacePersistsAsync(client, new[] { SeedIds.EditorOther, SeedIds.EditorDemo });
        await RestoreOtherClientAsync(client);
    }

    [Fact]
    public async Task Editor_can_save_assigned_techs_and_grid_persists_on_reopen()
    {
        var client = await _factory.LoginAsync("editor@bisconsultants.local");
        await AssertReplacePersistsAsync(client, new[] { SeedIds.OrgAdminOther, SeedIds.EditorOther });
        await RestoreOtherClientAsync(client);
    }

    [Fact]
    public async Task Global_admin_still_can_replace_assigned_techs()
    {
        var client = await _factory.LoginAsync("admin@bisconsultants.local");
        await AssertReplacePersistsAsync(client, new[] { SeedIds.EditorOther });
        await RestoreOtherClientAsync(client);
    }

    [Fact]
    public async Task Multi_technician_assignment_is_preserved()
    {
        var client = await _factory.LoginAsync("editor@bisconsultants.local");
        var assigned = new[] { SeedIds.EditorOther, SeedIds.OrgAdminOther, SeedIds.EditorDemo };
        var saved = await client.PutAsJsonAsync($"/api/admin/organizations/{SeedIds.OtherClient}", new
        {
            name = "Other Client",
            assignedTechIds = assigned
        });
        saved.StatusCode.Should().Be(HttpStatusCode.OK);
        TechIds(await saved.ReadJsonAsync()).Should().BeEquivalentTo(assigned);

        var reopened = TechIds(await OrgAsync(client, SeedIds.OtherClient));
        reopened.Should().BeEquivalentTo(assigned);
        reopened.Should().HaveCount(3);

        await RestoreOtherClientAsync(client);
    }

    [Fact]
    public async Task Viewer_and_anonymous_cannot_manage_assigned_techs()
    {
        var viewer = await _factory.LoginAsync("viewer@bisconsultants.local");
        (await viewer.GetAsync("/api/admin/organizations")).StatusCode.Should().Be(HttpStatusCode.Forbidden);
        (await viewer.PutAsJsonAsync($"/api/admin/organizations/{SeedIds.DemoClient}", new
        {
            name = "Demo Client",
            assignedTechIds = new[] { SeedIds.ViewerDemo }
        })).StatusCode.Should().Be(HttpStatusCode.Forbidden);

        var anon = _factory.CreateClient();
        (await anon.PutAsJsonAsync($"/api/admin/organizations/{SeedIds.DemoClient}", new
        {
            name = "Demo Client",
            assignedTechIds = new[] { SeedIds.EditorDemo }
        })).StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Changing_assigned_techs_does_not_reassign_existing_work_items()
    {
        var client = await _factory.LoginAsync("editor@bisconsultants.local");
        var before = await (await client.GetAsync($"/api/work-items/{SeedIds.OtherPlat}")).ReadJsonAsync();
        before.GetProperty("assignedToUserId").GetGuid().Should().Be(SeedIds.EditorOther);

        (await client.PutAsJsonAsync($"/api/admin/organizations/{SeedIds.OtherClient}", new
        {
            name = "Other Client",
            assignedTechIds = new[] { SeedIds.OrgAdminOther }
        })).StatusCode.Should().Be(HttpStatusCode.OK);

        var after = await (await client.GetAsync($"/api/work-items/{SeedIds.OtherPlat}")).ReadJsonAsync();
        after.GetProperty("assignedToUserId").GetGuid().Should().Be(SeedIds.EditorOther);
        after.GetProperty("assignedToName").GetString().Should().Be(before.GetProperty("assignedToName").GetString());

        await RestoreOtherClientAsync(client);
    }

    [Fact]
    public async Task Editor_cannot_change_unrelated_org_fields()
    {
        var client = await _factory.LoginAsync("editor@bisconsultants.local");
        var response = await client.PutAsJsonAsync($"/api/admin/organizations/{SeedIds.OtherClient}", new
        {
            name = "Hacked Client",
            code = "HACKED",
            isActive = false,
            timeReportCardsVisible = true,
            parcelTotalRealAccounts = 99,
            parcelWithOwnership = 9
        });
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var json = await response.ReadJsonAsync();
        json.GetProperty("name").GetString().Should().Be("Other Client");
        json.GetProperty("code").GetString().Should().Be("OTHERCLIENT");
        json.GetProperty("isActive").GetBoolean().Should().BeTrue();
        json.GetProperty("timeReportCardsVisible").GetBoolean().Should().BeFalse();
    }

    [Fact]
    public async Task Editor_can_list_organizations_but_not_users_or_upload_links()
    {
        var client = await _factory.LoginAsync("editor@bisconsultants.local");
        var list = await client.GetAsync("/api/admin/organizations");
        list.StatusCode.Should().Be(HttpStatusCode.OK);
        (await list.ReadJsonAsync()).EnumerateArray()
            .Select(x => x.GetProperty("name").GetString())
            .Should().Contain("Demo Client")
            .And.Contain("Other Client");

        (await client.GetAsync("/api/admin/users")).StatusCode.Should().Be(HttpStatusCode.Forbidden);
        (await client.GetAsync($"/api/admin/organizations/{SeedIds.DemoClient}/upload-link"))
            .StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    private static async Task AssertReplacePersistsAsync(HttpClient client, Guid[] assigned)
    {
        var saved = await client.PutAsJsonAsync($"/api/admin/organizations/{SeedIds.OtherClient}", new
        {
            name = "Other Client",
            assignedTechIds = assigned
        });
        saved.StatusCode.Should().Be(HttpStatusCode.OK, await saved.Content.ReadAsStringAsync());
        TechIds(await saved.ReadJsonAsync()).Should().BeEquivalentTo(assigned);

        var reopened = await OrgAsync(client, SeedIds.OtherClient);
        TechIds(reopened).Should().BeEquivalentTo(assigned);
    }

    private static async Task RestoreOtherClientAsync(HttpClient client)
    {
        var restore = await client.PutAsJsonAsync($"/api/admin/organizations/{SeedIds.OtherClient}", new
        {
            name = "Other Client",
            assignedTechIds = new[] { SeedIds.EditorOther, SeedIds.OrgAdminOther }
        });
        restore.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    private static async Task<JsonElement> OrgAsync(HttpClient client, Guid id)
    {
        var list = await (await client.GetAsync("/api/admin/organizations")).ReadJsonAsync();
        return list.EnumerateArray().Single(x => x.GetProperty("id").GetGuid() == id);
    }

    private static Guid[] TechIds(JsonElement org) =>
        org.GetProperty("assignedTechs").EnumerateArray()
            .Select(x => x.GetProperty("id").GetGuid())
            .ToArray();
}
