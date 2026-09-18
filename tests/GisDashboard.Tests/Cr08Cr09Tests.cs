using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using FluentAssertions;
using GisDashboard.Application.WorkItems;
using GisDashboard.Infrastructure.Persistence;

namespace GisDashboard.Tests;

/// <summary>
/// CR08 — new uploads auto-assign Assigned to the org primary Assigned technician,
/// else first Assigned tech, else unassigned and visible/actionable for staff.
/// QC01 org-tech edits do not rewrite existing document Assigned to.
/// CR09 — Assigned technician (org default) stays distinct from Assigned to (work item).
/// </summary>
public sealed class Cr08Cr09Tests : IClassFixture<ApiFactory>
{
    private readonly ApiFactory _factory;

    public Cr08Cr09Tests(ApiFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public void ResolveDefault_uses_primary_then_first_then_unassigned()
    {
        var primary = Guid.Parse("11111111-1111-1111-1111-111111111111");
        var firstByName = Guid.Parse("22222222-2222-2222-2222-222222222222");
        var laterByName = Guid.Parse("33333333-3333-3333-3333-333333333333");

        OrgAssignee.ResolveDefault([
            (laterByName, "Zed Tech", false, true),
            (firstByName, "Alex Tech", false, true),
            (primary, "Primary Tech", true, true)
        ]).Should().Be(primary, "Fail if: primary Assigned technician is not used for new uploads.");

        OrgAssignee.ResolveDefault([
            (laterByName, "Zed Tech", false, true),
            (firstByName, "Alex Tech", false, true)
        ]).Should().Be(firstByName, "Fail if: first Assigned tech is skipped when no primary is set.");

        OrgAssignee.ResolveDefault([
            (primary, "Primary Tech", true, false),
            (firstByName, "Alex Tech", false, true)
        ]).Should().Be(firstByName, "Fail if: an inactive primary leaves new uploads with no path.");

        OrgAssignee.ResolveDefault([]).Should().BeNull("Fail if: orgs with no Assigned technician invent an assignee.");
        OrgAssignee.ResolveDefault([
            (primary, "Primary Tech", true, false)
        ]).Should().BeNull();
    }

    [Fact]
    public async Task Upload_auto_assigns_primary_assigned_technician()
    {
        var admin = await _factory.LoginAsync("admin@bisconsultants.local");
        await SetOtherTechsAsync(admin, [SeedIds.OrgAdminOther, SeedIds.EditorOther], SeedIds.OrgAdminOther);

        var response = await UploadAsync(admin, SeedIds.OtherClient, "Other Client", "cr08-primary.pdf");
        response.StatusCode.Should().Be(HttpStatusCode.OK, await response.Content.ReadAsStringAsync());
        var json = await response.ReadJsonAsync();
        json.GetProperty("assignedToUserId").GetGuid().Should().Be(SeedIds.OrgAdminOther,
            "Fail if: orgs with a primary Assigned technician still leave new uploads unassigned.");
        json.GetProperty("assignedToName").GetString().Should().NotBeNullOrWhiteSpace();

        await RestoreOtherClientAsync(admin);
    }

    [Fact]
    public async Task Upload_auto_assigns_first_assigned_tech_when_primary_is_cleared_to_first()
    {
        var admin = await _factory.LoginAsync("admin@bisconsultants.local");
        await SetOtherTechsAsync(admin, [], null);
        await SetOtherTechsAsync(admin, [SeedIds.EditorOther, SeedIds.OrgAdminOther], null);

        var response = await UploadAsync(admin, SeedIds.OtherClient, "Other Client", "cr08-first-tech.pdf");
        response.StatusCode.Should().Be(HttpStatusCode.OK, await response.Content.ReadAsStringAsync());
        (await response.ReadJsonAsync()).GetProperty("assignedToUserId").GetGuid().Should().Be(SeedIds.EditorOther,
            "Fail if: orgs with Assigned tech(s) still leave new uploads unassigned.");

        await RestoreOtherClientAsync(admin);
    }

    [Fact]
    public async Task Upload_to_org_without_assigned_tech_stays_unassigned_and_staff_can_assign()
    {
        var admin = await _factory.LoginAsync("admin@bisconsultants.local");
        var created = await admin.PostAsJsonAsync("/api/admin/organizations", new
        {
            name = "CR08 No Tech Client",
            code = $"CR08NONE{Guid.NewGuid():N}"[..16]
        });
        created.StatusCode.Should().Be(HttpStatusCode.OK, await created.Content.ReadAsStringAsync());
        var org = await created.ReadJsonAsync();
        var orgId = org.GetProperty("id").GetGuid();
        org.GetProperty("assignedTechs").GetArrayLength().Should().Be(0);

        var uploaded = await UploadAsync(admin, orgId, "CR08 No Tech Client", "cr08-unassigned.pdf");
        uploaded.StatusCode.Should().Be(HttpStatusCode.OK, await uploaded.Content.ReadAsStringAsync());
        var item = await uploaded.ReadJsonAsync();
        item.TryGetProperty("assignedToUserId", out var assigned).Should().BeTrue();
        assigned.ValueKind.Should().Be(JsonValueKind.Null,
            "Fail if: orgs with no Assigned technician invent an assignee.");
        var id = item.GetProperty("id").GetGuid();

        var editor = await _factory.LoginAsync("editor@bisconsultants.local");
        var unassigned = await (await editor.GetAsync("/api/work-items?unassignedOnly=true&pageSize=100")).ReadJsonAsync();
        unassigned.GetProperty("items").EnumerateArray().Select(x => x.GetProperty("id").GetGuid())
            .Should().Contain(id, "Fail if: unassigned work has no path for authorized staff.");
        unassigned.GetProperty("buckets").GetProperty("unassigned").GetInt32().Should().BeGreaterThan(0);

        var bucket = await (await editor.GetAsync("/api/work-items?bucket=unassigned&pageSize=100")).ReadJsonAsync();
        bucket.GetProperty("items").EnumerateArray().Select(x => x.GetProperty("id").GetGuid())
            .Should().Contain(id);

        var mine = await (await editor.GetAsync(
            $"/api/work-items?assignedToUserId={SeedIds.EditorDemo}&pageSize=100")).ReadJsonAsync();
        mine.GetProperty("items").EnumerateArray().Select(x => x.GetProperty("id").GetGuid())
            .Should().NotContain(id);
        mine.GetProperty("buckets").GetProperty("unassigned").GetInt32().Should().BeGreaterThan(0,
            "Fail if: personal Assigned-to queue hides that unassigned work exists.");

        var patched = await editor.PatchAsJsonAsync($"/api/work-items/{id}", new { assignedToUserId = SeedIds.EditorDemo });
        patched.StatusCode.Should().Be(HttpStatusCode.OK, await patched.Content.ReadAsStringAsync());
        (await patched.ReadJsonAsync()).GetProperty("assignedToUserId").GetGuid().Should().Be(SeedIds.EditorDemo);
    }

    [Fact]
    public async Task Changing_org_assigned_tech_does_not_reassign_existing_docs()
    {
        var admin = await _factory.LoginAsync("admin@bisconsultants.local");
        await SetOtherTechsAsync(admin, [SeedIds.OrgAdminOther, SeedIds.EditorOther], SeedIds.OrgAdminOther);

        var first = await UploadAsync(admin, SeedIds.OtherClient, "Other Client", "cr08-keep-assignee.pdf");
        first.StatusCode.Should().Be(HttpStatusCode.OK);
        var existingId = (await first.ReadJsonAsync()).GetProperty("id").GetGuid();

        await SetOtherTechsAsync(admin, [SeedIds.EditorOther], SeedIds.EditorOther);

        var after = await (await admin.GetAsync($"/api/work-items/{existingId}")).ReadJsonAsync();
        after.GetProperty("assignedToUserId").GetGuid().Should().Be(SeedIds.OrgAdminOther,
            "Fail if: reassignment of org tech rewrites historical Assigned to.");

        var next = await UploadAsync(admin, SeedIds.OtherClient, "Other Client", "cr08-future-only.pdf");
        next.StatusCode.Should().Be(HttpStatusCode.OK);
        (await next.ReadJsonAsync()).GetProperty("assignedToUserId").GetGuid().Should().Be(SeedIds.EditorOther,
            "Fail if: later uploads do not follow the new org Assigned technician.");

        await RestoreOtherClientAsync(admin);
    }

    [Fact]
    public async Task Viewer_cannot_use_unassigned_assignee_filter()
    {
        var viewer = await _factory.LoginAsync("viewer@bisconsultants.local");
        var json = await (await viewer.GetAsync("/api/work-items?unassignedOnly=true&pageSize=100")).ReadJsonAsync();
        json.GetProperty("items").EnumerateArray().Should().NotBeEmpty();
        json.GetProperty("items").EnumerateArray()
            .Should().OnlyContain(x => x.GetProperty("organizationId").GetGuid() == SeedIds.DemoClient);
    }

    [Fact]
    public async Task Settings_name_cr08_and_cr09_field_meanings()
    {
        var client = await _factory.LoginAsync("admin@bisconsultants.local");
        var features = (await (await client.GetAsync("/api/settings")).ReadJsonAsync()).GetProperty("features");

        var assignment = features.GetProperty("documentAssignment").GetProperty("note").GetString();
        assignment.Should().Contain("CR08");
        assignment.Should().Contain("primary");
        assignment.Should().Contain("Unassigned");
        assignment.Should().Contain("QC01");

        var fields = features.GetProperty("assignmentFields").GetProperty("note").GetString();
        fields.Should().Contain("CR09");
        fields.Should().Contain("Assigned technician");
        fields.Should().Contain("Assigned to");
        fields.Should().Contain("QC01");

        features.GetProperty("myQueue").GetProperty("note").GetString().Should().Contain("Assigned to");
        features.GetProperty("uploadDocuments").GetProperty("note").GetString().Should().Contain("CR08");
        features.GetProperty("dashboardAssignee").GetProperty("note").GetString().Should().Contain("CR09");
    }

    private static async Task SetOtherTechsAsync(HttpClient client, Guid[] assigned, Guid? primary)
    {
        var body = new Dictionary<string, object?>
        {
            ["name"] = "Other Client",
            ["assignedTechIds"] = assigned
        };
        if (primary is { } id)
        {
            body["primaryAssignedTechId"] = id;
        }

        var saved = await client.PutAsJsonAsync($"/api/admin/organizations/{SeedIds.OtherClient}", body);
        saved.StatusCode.Should().Be(HttpStatusCode.OK, await saved.Content.ReadAsStringAsync());
    }

    private static async Task RestoreOtherClientAsync(HttpClient client) =>
        await SetOtherTechsAsync(client, [SeedIds.EditorOther, SeedIds.OrgAdminOther], SeedIds.EditorOther);

    private static async Task<HttpResponseMessage> UploadAsync(
        HttpClient client,
        Guid organizationId,
        string organizationName,
        string fileName)
    {
        using var form = new MultipartFormDataContent();
        form.Add(new StringContent(organizationId.ToString()), "organizationId");
        form.Add(new StringContent(organizationName), "organizationName");
        form.Add(new StringContent(SeedIds.TypeDeed.ToString()), "documentTypeId");
        form.Add(new StringContent("Deed"), "documentTypeName");
        form.Add(new StringContent(fileName), "title");
        var file = new ByteArrayContent([0x25, 0x50, 0x44, 0x46, 0x2D]);
        file.Headers.ContentType = new MediaTypeHeaderValue("application/pdf");
        form.Add(file, "file", fileName);
        return await client.PostAsync("/api/work-items", form);
    }
}
