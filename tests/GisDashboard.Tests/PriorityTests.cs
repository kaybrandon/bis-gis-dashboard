using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using FluentAssertions;
using GisDashboard.Infrastructure.Persistence;

namespace GisDashboard.Tests;

public sealed class PriorityTests : IClassFixture<ApiFactory>
{
    private readonly ApiFactory _factory;

    public PriorityTests(ApiFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task Org_admin_can_set_priority_and_note_on_own_item()
    {
        var client = await _factory.LoginAsync("admin@democlient.local");
        var response = await client.PatchAsJsonAsync($"/api/work-items/{SeedIds.DemoPlat}", new
        {
            isPriority = true,
            priorityNote = "needed by Friday"
        });
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var json = await response.ReadJsonAsync();
        json.GetProperty("isPriority").GetBoolean().Should().BeTrue();
        json.GetProperty("priorityNote").GetString().Should().Be("needed by Friday");
        json.GetProperty("canSetPriority").GetBoolean().Should().BeTrue();

        await client.PatchAsJsonAsync($"/api/work-items/{SeedIds.DemoPlat}", new { isPriority = false });
    }

    [Fact]
    public async Task Viewer_cannot_mark_priority()
    {
        var client = await _factory.LoginAsync("viewer@bisconsultants.local");
        var detail = await (await client.GetAsync($"/api/work-items/{SeedIds.DemoSurvey}")).ReadJsonAsync();
        detail.GetProperty("canSetPriority").GetBoolean().Should().BeFalse();

        (await client.PatchAsJsonAsync($"/api/work-items/{SeedIds.DemoSurvey}", new
        {
            isPriority = true,
            priorityNote = "viewer should not set this"
        })).StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task Editor_sees_priority_bucket_and_can_clear()
    {
        var admin = await _factory.LoginAsync("admin@democlient.local");
        (await admin.PatchAsJsonAsync($"/api/work-items/{SeedIds.DemoSurvey}", new
        {
            isPriority = true,
            priorityNote = "survey first"
        })).StatusCode.Should().Be(HttpStatusCode.OK);

        var editor = await _factory.LoginAsync("editor@bisconsultants.local");
        var list = await (await editor.GetAsync("/api/work-items?bucket=priority&pageSize=100")).ReadJsonAsync();
        list.GetProperty("buckets").GetProperty("priority").GetInt32().Should().BeGreaterThanOrEqualTo(1);
        list.GetProperty("items").EnumerateArray()
            .Should().Contain(x => x.GetProperty("id").GetGuid() == SeedIds.DemoSurvey);
        list.GetProperty("items").EnumerateArray()
            .Should().OnlyContain(x => x.GetProperty("isPriority").GetBoolean());

        var cleared = await editor.PatchAsJsonAsync($"/api/work-items/{SeedIds.DemoSurvey}", new { isPriority = false });
        cleared.StatusCode.Should().Be(HttpStatusCode.OK);
        (await cleared.ReadJsonAsync()).GetProperty("isPriority").GetBoolean().Should().BeFalse();

        var after = await (await editor.GetAsync("/api/work-items?bucket=priority&pageSize=100")).ReadJsonAsync();
        after.GetProperty("items").EnumerateArray()
            .Should().NotContain(x => x.GetProperty("id").GetGuid() == SeedIds.DemoSurvey);
    }

    [Fact]
    public async Task Token_upload_priority_notifies_assigned_tech()
    {
        var admin = await _factory.LoginAsync("admin@bisconsultants.local");
        var link = await (await admin.GetAsync($"/api/admin/organizations/{SeedIds.DemoClient}/upload-link")).ReadJsonAsync();
        var token = link.GetProperty("token").GetString()!;

        var anon = _factory.CreateClient();
        using var form = new MultipartFormDataContent();
        form.Add(new StringContent(SeedIds.TypePlat.ToString()), "documentTypeId");
        form.Add(new StringContent("Plat"), "documentTypeName");
        form.Add(new StringContent("true"), "isPriority");
        form.Add(new StringContent("needed by Friday"), "priorityNote");
        var file = new ByteArrayContent([0x25, 0x50, 0x44, 0x46, 0x2D]);
        file.Headers.ContentType = new MediaTypeHeaderValue("application/pdf");
        form.Add(file, "file", "priority-token.pdf");
        var uploaded = await anon.PostAsync($"/api/public/uploads/{token}", form);
        uploaded.StatusCode.Should().Be(HttpStatusCode.OK, await uploaded.Content.ReadAsStringAsync());
        var id = (await uploaded.ReadJsonAsync()).GetProperty("id").GetGuid();

        var detail = await (await admin.GetAsync($"/api/work-items/{id}")).ReadJsonAsync();
        detail.GetProperty("isPriority").GetBoolean().Should().BeTrue();
        detail.GetProperty("priorityNote").GetString().Should().Be("needed by Friday");
        detail.GetProperty("priorityRequestedByName").GetString().Should().Be("Upload link");

        var editor = await _factory.LoginAsync("editor@bisconsultants.local");
        var alerts = await (await editor.GetAsync("/api/notifications")).ReadJsonAsync();
        alerts.GetProperty("unreadCount").GetInt32().Should().BeGreaterThanOrEqualTo(1);
        alerts.GetProperty("items").EnumerateArray().Should().Contain(x =>
            x.GetProperty("kind").GetString() == "priority" &&
            x.GetProperty("workItemId").GetGuid() == id &&
            x.GetProperty("body").GetString()!.Contains("priority-token.pdf") &&
            x.GetProperty("body").GetString()!.Contains("needed by Friday"));

        var orgAdmin = await _factory.LoginAsync("admin@democlient.local");
        var adminAlerts = await (await orgAdmin.GetAsync("/api/notifications")).ReadJsonAsync();
        adminAlerts.GetProperty("items").EnumerateArray().Should().Contain(x =>
            x.GetProperty("kind").GetString() == "priority" &&
            x.GetProperty("workItemId").GetGuid() == id);

        var viewer = await _factory.LoginAsync("viewer@bisconsultants.local");
        var viewerAlerts = await (await viewer.GetAsync("/api/notifications")).ReadJsonAsync();
        viewerAlerts.GetProperty("items").EnumerateArray()
            .Should().NotContain(x => x.GetProperty("workItemId").GetGuid() == id);

        var gaAlerts = await (await admin.GetAsync("/api/notifications")).ReadJsonAsync();
        gaAlerts.GetProperty("items").EnumerateArray().Should().Contain(x =>
            x.GetProperty("kind").GetString() == "priority" &&
            x.GetProperty("workItemId").GetGuid() == id);
    }

    [Fact]
    public async Task Global_admin_save_priority_creates_bell_alert_for_self_and_tech()
    {
        var global = await _factory.LoginAsync("admin@bisconsultants.local");
        (await global.PatchAsJsonAsync($"/api/work-items/{SeedIds.DemoPlat}", new
        {
            isPriority = true,
            priorityNote = "needed by Friday"
        })).StatusCode.Should().Be(HttpStatusCode.OK);

        var gaAlerts = await (await global.GetAsync("/api/notifications")).ReadJsonAsync();
        gaAlerts.GetProperty("items").EnumerateArray().Should().Contain(x =>
            x.GetProperty("kind").GetString() == "priority" &&
            x.GetProperty("workItemId").GetGuid() == SeedIds.DemoPlat &&
            x.GetProperty("title").GetString()!.Length > 0 &&
            x.GetProperty("body").GetString()!.Contains("needed by Friday"));

        var editor = await _factory.LoginAsync("editor@bisconsultants.local");
        var techAlerts = await (await editor.GetAsync("/api/notifications")).ReadJsonAsync();
        techAlerts.GetProperty("items").EnumerateArray().Should().Contain(x =>
            x.GetProperty("workItemId").GetGuid() == SeedIds.DemoPlat);

        var again = await global.PatchAsJsonAsync($"/api/work-items/{SeedIds.DemoPlat}", new
        {
            isPriority = true,
            priorityNote = "needed by Friday — still priority"
        });
        again.StatusCode.Should().Be(HttpStatusCode.OK);
        var upserted = await (await global.GetAsync("/api/notifications")).ReadJsonAsync();
        upserted.GetProperty("items").EnumerateArray()
            .Count(x => x.GetProperty("workItemId").GetGuid() == SeedIds.DemoPlat)
            .Should().Be(1);
        upserted.GetProperty("items").EnumerateArray().Should().Contain(x =>
            x.GetProperty("workItemId").GetGuid() == SeedIds.DemoPlat &&
            x.GetProperty("body").GetString()!.Contains("still priority"));

        await global.PatchAsJsonAsync($"/api/work-items/{SeedIds.DemoPlat}", new { isPriority = false });
    }

    [Fact]
    public async Task Demo_admin_can_priority_other_org_item()
    {
        var client = await _factory.LoginAsync("admin@democlient.local");
        (await client.PatchAsJsonAsync($"/api/work-items/{SeedIds.OtherPlat}", new
        {
            isPriority = true,
            priorityNote = "cross-org"
        })).StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task Dashboard_priority_kpi_increments()
    {
        var client = await _factory.LoginAsync("admin@bisconsultants.local");
        var before = Kpi((await (await client.GetAsync("/api/dashboard")).ReadJsonAsync()), "priority");

        using var form = new MultipartFormDataContent();
        form.Add(new StringContent(SeedIds.DemoClient.ToString()), "organizationId");
        form.Add(new StringContent("Demo Client"), "organizationName");
        form.Add(new StringContent(SeedIds.TypeDeed.ToString()), "documentTypeId");
        form.Add(new StringContent("Deed"), "documentTypeName");
        form.Add(new StringContent("KPI priority check"), "title");
        var file = new ByteArrayContent([0x25, 0x50, 0x44, 0x46, 0x2D]);
        file.Headers.ContentType = new MediaTypeHeaderValue("application/pdf");
        form.Add(file, "file", "kpi-priority.pdf");
        var uploaded = await client.PostAsync("/api/work-items", form);
        uploaded.StatusCode.Should().Be(HttpStatusCode.OK);
        var id = (await uploaded.ReadJsonAsync()).GetProperty("id").GetGuid();

        var mid = Kpi((await (await client.GetAsync("/api/dashboard")).ReadJsonAsync()), "priority");
        mid.Should().Be(before);

        var patch = await client.PatchAsJsonAsync($"/api/work-items/{id}", new
        {
            isPriority = true,
            priorityNote = "board packet"
        });
        patch.StatusCode.Should().Be(HttpStatusCode.OK);

        var after = Kpi((await (await client.GetAsync("/api/dashboard")).ReadJsonAsync()), "priority");
        after.Should().Be(before + 1);
    }

    [Fact]
    public async Task Seed_assigns_demo_tech_and_global_admin_can_replace()
    {
        var client = await _factory.LoginAsync("admin@bisconsultants.local");
        var list = await (await client.GetAsync("/api/admin/organizations")).ReadJsonAsync();
        var demo = list.EnumerateArray().Single(x => x.GetProperty("id").GetGuid() == SeedIds.DemoClient);
        demo.GetProperty("assignedTechs").EnumerateArray()
            .Should().Contain(x => x.GetProperty("id").GetGuid() == SeedIds.EditorDemo);

        var settings = await (await client.GetAsync("/api/settings")).ReadJsonAsync();
        settings.GetProperty("features").GetProperty("priorityWork").GetProperty("enabled").GetBoolean().Should().BeTrue();

        var updated = await client.PutAsJsonAsync($"/api/admin/organizations/{SeedIds.OtherClient}", new
        {
            name = "Other Client",
            code = "OTHERCLIENT",
            isActive = true,
            assignedTechIds = new[] { SeedIds.EditorOther }
        });
        updated.StatusCode.Should().Be(HttpStatusCode.OK);
        (await updated.ReadJsonAsync()).GetProperty("assignedTechs").EnumerateArray()
            .Should().ContainSingle(x => x.GetProperty("id").GetGuid() == SeedIds.EditorOther);

        await client.PutAsJsonAsync($"/api/admin/organizations/{SeedIds.OtherClient}", new
        {
            name = "Other Client",
            code = "OTHERCLIENT",
            isActive = true,
            assignedTechIds = Array.Empty<Guid>()
        });
    }

    [Fact]
    public async Task Org_admin_can_change_assigned_techs()
    {
        var client = await _factory.LoginAsync("admin@democlient.local");
        var response = await client.PutAsJsonAsync($"/api/admin/organizations/{SeedIds.OtherClient}", new
        {
            name = "Other Client",
            assignedTechIds = new[] { SeedIds.EditorOther, SeedIds.OrgAdminOther }
        });
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        (await response.ReadJsonAsync()).GetProperty("assignedTechs").EnumerateArray()
            .Select(x => x.GetProperty("id").GetGuid())
            .Should().BeEquivalentTo(new[] { SeedIds.EditorOther, SeedIds.OrgAdminOther });
    }

    [Fact]
    public async Task Priority_notifies_org_techs_and_assignee_not_every_editor()
    {
        var global = await _factory.LoginAsync("admin@bisconsultants.local");
        (await global.PutAsJsonAsync($"/api/admin/organizations/{SeedIds.OtherClient}", new
        {
            name = "Other Client",
            code = "OTHERCLIENT",
            isActive = true,
            assignedTechIds = new[] { SeedIds.EditorOther }
        })).StatusCode.Should().Be(HttpStatusCode.OK);

        using var form = new MultipartFormDataContent();
        form.Add(new StringContent(SeedIds.OtherClient.ToString()), "organizationId");
        form.Add(new StringContent("Other Client"), "organizationName");
        form.Add(new StringContent(SeedIds.TypePlat.ToString()), "documentTypeId");
        form.Add(new StringContent("Plat"), "documentTypeName");
        form.Add(new StringContent(SeedIds.Admin.ToString()), "assignedToUserId");
        form.Add(new StringContent("Other-priority-assignee.pdf"), "title");
        var file = new ByteArrayContent([0x25, 0x50, 0x44, 0x46, 0x2D]);
        file.Headers.ContentType = new MediaTypeHeaderValue("application/pdf");
        form.Add(file, "file", "other-priority-assignee.pdf");
        var uploaded = await global.PostAsync("/api/work-items", form);
        uploaded.StatusCode.Should().Be(HttpStatusCode.OK, await uploaded.Content.ReadAsStringAsync());
        var id = (await uploaded.ReadJsonAsync()).GetProperty("id").GetGuid();

        var otherAdmin = await _factory.LoginAsync("admin@otherclient.local");
        (await otherAdmin.PatchAsJsonAsync($"/api/work-items/{id}", new
        {
            isPriority = true,
            priorityNote = "board packet Friday"
        })).StatusCode.Should().Be(HttpStatusCode.OK);

        var orgTech = await _factory.LoginAsync("editor.other@bisconsultants.local");
        var techAlerts = await (await orgTech.GetAsync("/api/notifications")).ReadJsonAsync();
        techAlerts.GetProperty("items").EnumerateArray().Should().Contain(x =>
            x.GetProperty("workItemId").GetGuid() == id &&
            x.GetProperty("body").GetString()!.Contains("Other Client") &&
            x.GetProperty("body").GetString()!.Contains("board packet Friday"));

        var assigneeAlerts = await (await global.GetAsync("/api/notifications")).ReadJsonAsync();
        assigneeAlerts.GetProperty("items").EnumerateArray()
            .Should().Contain(x => x.GetProperty("workItemId").GetGuid() == id);

        var otherOrgEditor = await _factory.LoginAsync("editor@bisconsultants.local");
        var otherAlerts = await (await otherOrgEditor.GetAsync("/api/notifications")).ReadJsonAsync();
        otherAlerts.GetProperty("items").EnumerateArray()
            .Should().NotContain(x => x.GetProperty("workItemId").GetGuid() == id);

        var actorAlerts = await (await otherAdmin.GetAsync("/api/notifications")).ReadJsonAsync();
        actorAlerts.GetProperty("items").EnumerateArray()
            .Should().NotContain(x => x.GetProperty("workItemId").GetGuid() == id);

        await global.PutAsJsonAsync($"/api/admin/organizations/{SeedIds.OtherClient}", new
        {
            name = "Other Client",
            code = "OTHERCLIENT",
            isActive = true,
            assignedTechIds = Array.Empty<Guid>()
        });
    }

    [Fact]
    public async Task Notifications_require_auth()
    {
        var anon = _factory.CreateClient();
        (await anon.GetAsync("/api/notifications")).StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    private static int Kpi(System.Text.Json.JsonElement json, string key) =>
        json.GetProperty("kpis").EnumerateArray()
            .Single(x => x.GetProperty("key").GetString() == key)
            .GetProperty("count").GetInt32();
}
