using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using GisDashboard.Domain;
using GisDashboard.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace GisDashboard.Tests;

/// <summary>
/// QC03 — Auto-associate staff with every organization.
/// Viewer/Uploader stay on assigned orgs. Global Admin implicit all-org access is unchanged (WL01).
/// </summary>
public sealed class Qc03StaffOrganizationTests : IClassFixture<ApiFactory>
{
    private readonly ApiFactory _factory;

    public Qc03StaffOrganizationTests(ApiFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public void Staff_roles_receive_all_org_membership_clients_do_not()
    {
        Roles.ReceivesAllOrganizationMembership(Roles.Editor).Should().BeTrue();
        Roles.ReceivesAllOrganizationMembership(Roles.Administrator).Should().BeTrue();
        Roles.ReceivesAllOrganizationMembership(Roles.GlobalAdministrator).Should().BeFalse();
        Roles.ReceivesAllOrganizationMembership(Roles.Viewer).Should().BeFalse();
        Roles.ReceivesAllOrganizationMembership(Roles.Uploader).Should().BeFalse();
        Roles.RequiresAssignedOrganizations(Roles.Viewer).Should().BeTrue();
        Roles.RequiresAssignedOrganizations(Roles.Uploader).Should().BeTrue();
        Roles.RequiresAssignedOrganizations(Roles.Editor).Should().BeFalse();
        Roles.CanSeeAllOrganizations(Roles.GlobalAdministrator).Should().BeTrue();
    }

    [Fact]
    public async Task Creating_editor_without_org_picker_grants_every_current_organization()
    {
        var admin = await _factory.LoginAsync("admin@bisconsultants.local");
        var created = await admin.PostAsJsonAsync("/api/admin/users", new
        {
            email = "qc03.editor@bisconsultants.local",
            password = "Demo!Gis2026",
            displayName = "QC03 Editor",
            role = "Editor"
        });
        created.StatusCode.Should().Be(HttpStatusCode.OK, await created.Content.ReadAsStringAsync());
        var json = await created.ReadJsonAsync();
        json.GetProperty("role").GetString().Should().Be("Editor");
        var allOrgIds = await AllOrganizationIdsAsync(admin);
        OrgIds(json).Should().BeEquivalentTo(allOrgIds);

        var listed = await (await admin.GetAsync("/api/admin/users")).ReadJsonAsync();
        OrgIds(listed.EnumerateArray().Single(x => x.GetProperty("email").GetString() == "qc03.editor@bisconsultants.local"))
            .Should().BeEquivalentTo(allOrgIds);

        var editor = await _factory.LoginAsync("qc03.editor@bisconsultants.local");
        var me = await (await editor.GetAsync("/api/auth/me")).ReadJsonAsync();
        me.GetProperty("canSeeAllOrganizations").GetBoolean().Should().BeTrue();
        MeOrgIds(me).Should().BeEquivalentTo(allOrgIds);
        (await editor.GetAsync($"/api/work-items/{SeedIds.DemoPlat}")).StatusCode.Should().Be(HttpStatusCode.OK);
        (await editor.GetAsync($"/api/work-items/{SeedIds.OtherPlat}")).StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task Creating_administrator_without_orgs_grants_every_current_organization()
    {
        var admin = await _factory.LoginAsync("admin@bisconsultants.local");
        var created = await admin.PostAsJsonAsync("/api/admin/users", new
        {
            email = "qc03.admin@bisconsultants.local",
            password = "Demo!Gis2026",
            displayName = "QC03 Admin",
            role = "Administrator",
            organizationIds = Array.Empty<Guid>()
        });
        created.StatusCode.Should().Be(HttpStatusCode.OK, await created.Content.ReadAsStringAsync());
        OrgIds(await created.ReadJsonAsync()).Should().BeEquivalentTo(await AllOrganizationIdsAsync(admin));
    }

    [Fact]
    public async Task New_organization_is_auto_available_to_existing_editor_and_administrator()
    {
        var admin = await _factory.LoginAsync("admin@bisconsultants.local");
        var created = await admin.PostAsJsonAsync("/api/admin/organizations", new
        {
            name = "QC03 New Client",
            code = "QC03NEW"
        });
        created.StatusCode.Should().Be(HttpStatusCode.OK, await created.Content.ReadAsStringAsync());
        var org = await created.ReadJsonAsync();
        var orgId = org.GetProperty("id").GetGuid();
        org.GetProperty("members").EnumerateArray().Select(x => x.GetProperty("id").GetGuid())
            .Should().Contain(SeedIds.EditorDemo)
            .And.Contain(SeedIds.OrgAdminDemo)
            .And.Contain(SeedIds.EditorOther)
            .And.Contain(SeedIds.OrgAdminOther)
            .And.NotContain(SeedIds.ViewerDemo)
            .And.NotContain(SeedIds.UploaderDemo)
            .And.NotContain(SeedIds.Admin);
        org.GetProperty("assignedTechs").EnumerateArray()
            .Should().BeEmpty();

        var users = await (await admin.GetAsync("/api/admin/users")).ReadJsonAsync();
        OrgIds(users.EnumerateArray().Single(x => x.GetProperty("id").GetGuid() == SeedIds.EditorDemo))
            .Should().Contain(orgId);
        OrgIds(users.EnumerateArray().Single(x => x.GetProperty("id").GetGuid() == SeedIds.OrgAdminOther))
            .Should().Contain(orgId);
        OrgIds(users.EnumerateArray().Single(x => x.GetProperty("id").GetGuid() == SeedIds.ViewerDemo))
            .Should().Equal(SeedIds.DemoClient);
        OrgIds(users.EnumerateArray().Single(x => x.GetProperty("id").GetGuid() == SeedIds.UploaderDemo))
            .Should().Equal(SeedIds.DemoClient);

        var editor = await _factory.LoginAsync("editor@bisconsultants.local");
        var me = await (await editor.GetAsync("/api/auth/me")).ReadJsonAsync();
        MeOrgIds(me).Should().Contain(orgId);
        var lookups = await (await editor.GetAsync("/api/lookups/organizations")).ReadJsonAsync();
        lookups.EnumerateArray().Select(x => x.GetProperty("id").GetGuid())
            .Should().Contain(orgId);
    }

    [Fact]
    public async Task Existing_partial_staff_membership_is_backfilled_and_survives_sign_in()
    {
        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var leftover = await db.UserOrganizations
                .Where(x => x.UserId == SeedIds.EditorDemo && x.OrganizationId == SeedIds.OtherClient)
                .ToListAsync();
            leftover.Should().NotBeEmpty("seed staff must already be associated with every org after startup");
            db.UserOrganizations.RemoveRange(leftover);
            await db.SaveChangesAsync();
        }

        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            await StaffOrganizationMembership.EnsureAsync(db);
        }

        var admin = await _factory.LoginAsync("admin@bisconsultants.local");
        var users = await (await admin.GetAsync("/api/admin/users")).ReadJsonAsync();
        var allOrgIds = await AllOrganizationIdsAsync(admin);
        OrgIds(users.EnumerateArray().Single(x => x.GetProperty("id").GetGuid() == SeedIds.EditorDemo))
            .Should().BeEquivalentTo(allOrgIds);

        var editor = await _factory.LoginAsync("editor@bisconsultants.local");
        var me = await (await editor.GetAsync("/api/auth/me")).ReadJsonAsync();
        MeOrgIds(me).Should().BeEquivalentTo(allOrgIds);
        (await editor.GetAsync($"/api/work-items/{SeedIds.OtherPlat}")).StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task Staff_org_list_cannot_be_shrunk_on_edit()
    {
        var admin = await _factory.LoginAsync("admin@bisconsultants.local");
        var response = await admin.PutAsJsonAsync($"/api/admin/users/{SeedIds.EditorOther}", new
        {
            displayName = "Casey Nguyen",
            email = "editor.other@bisconsultants.local",
            role = "Editor",
            organizationIds = new[] { SeedIds.OtherClient },
            isActive = true
        });
        response.StatusCode.Should().Be(HttpStatusCode.OK, await response.Content.ReadAsStringAsync());
        var allOrgIds = await AllOrganizationIdsAsync(admin);
        OrgIds(await response.ReadJsonAsync()).Should().BeEquivalentTo(allOrgIds);

        var listed = await (await admin.GetAsync("/api/admin/users")).ReadJsonAsync();
        OrgIds(listed.EnumerateArray().Single(x => x.GetProperty("id").GetGuid() == SeedIds.EditorOther))
            .Should().BeEquivalentTo(allOrgIds);
    }

    [Fact]
    public async Task Viewer_and_uploader_stay_on_assigned_organizations()
    {
        var admin = await _factory.LoginAsync("admin@bisconsultants.local");
        var missing = await admin.PostAsJsonAsync("/api/admin/users", new
        {
            email = "qc03.viewer.missing@democlient.local",
            password = "Demo!Gis2026",
            displayName = "QC03 Viewer Missing",
            role = "Viewer"
        });
        missing.StatusCode.Should().Be(HttpStatusCode.BadRequest);

        var viewer = await admin.PostAsJsonAsync("/api/admin/users", new
        {
            email = "qc03.viewer@democlient.local",
            password = "Demo!Gis2026",
            displayName = "QC03 Viewer",
            role = "Viewer",
            organizationIds = new[] { SeedIds.DemoClient }
        });
        viewer.StatusCode.Should().Be(HttpStatusCode.OK, await viewer.Content.ReadAsStringAsync());
        OrgIds(await viewer.ReadJsonAsync()).Should().Equal(SeedIds.DemoClient);

        var uploader = await admin.PostAsJsonAsync("/api/admin/users", new
        {
            email = "qc03.uploader@democlient.local",
            password = "Demo!Gis2026",
            displayName = "QC03 Uploader",
            role = "Uploader",
            organizationIds = new[] { SeedIds.OtherClient }
        });
        uploader.StatusCode.Should().Be(HttpStatusCode.OK, await uploader.Content.ReadAsStringAsync());
        OrgIds(await uploader.ReadJsonAsync()).Should().Equal(SeedIds.OtherClient);

        var viewerClient = await _factory.LoginAsync("qc03.viewer@democlient.local");
        var viewerMe = await (await viewerClient.GetAsync("/api/auth/me")).ReadJsonAsync();
        viewerMe.GetProperty("canSeeAllOrganizations").GetBoolean().Should().BeFalse();
        MeOrgIds(viewerMe).Should().Equal(SeedIds.DemoClient);
        (await viewerClient.GetAsync($"/api/work-items/{SeedIds.OtherPlat}")).StatusCode.Should().Be(HttpStatusCode.NotFound);

        var uploaderClient = await _factory.LoginAsync("qc03.uploader@democlient.local");
        var uploaderMe = await (await uploaderClient.GetAsync("/api/auth/me")).ReadJsonAsync();
        uploaderMe.GetProperty("canSeeAllOrganizations").GetBoolean().Should().BeFalse();
        MeOrgIds(uploaderMe).Should().Equal(SeedIds.OtherClient);
        (await uploaderClient.GetAsync($"/api/work-items/{SeedIds.DemoPlat}")).StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task Creating_staff_does_not_add_them_as_assigned_tech_on_every_org()
    {
        var admin = await _factory.LoginAsync("admin@bisconsultants.local");
        var created = await admin.PostAsJsonAsync("/api/admin/users", new
        {
            email = "qc03.editor.notech@bisconsultants.local",
            password = "Demo!Gis2026",
            displayName = "QC03 No Tech",
            role = "Editor"
        });
        created.StatusCode.Should().Be(HttpStatusCode.OK, await created.Content.ReadAsStringAsync());
        var userId = (await created.ReadJsonAsync()).GetProperty("id").GetGuid();

        var orgs = await (await admin.GetAsync("/api/admin/organizations")).ReadJsonAsync();
        foreach (var org in orgs.EnumerateArray())
        {
            org.GetProperty("assignedTechs").EnumerateArray()
                .Should().NotContain(x => x.GetProperty("id").GetGuid() == userId);
        }
    }

    private static async Task<List<Guid>> AllOrganizationIdsAsync(HttpClient admin)
    {
        var orgs = await (await admin.GetAsync("/api/admin/organizations")).ReadJsonAsync();
        return orgs.EnumerateArray().Select(x => x.GetProperty("id").GetGuid()).ToList();
    }

    private static IReadOnlyList<Guid> OrgIds(System.Text.Json.JsonElement user) =>
        user.GetProperty("organizations").EnumerateArray()
            .Select(x => x.GetProperty("organizationId").GetGuid())
            .ToList();

    private static IReadOnlyList<Guid> MeOrgIds(System.Text.Json.JsonElement me) =>
        me.GetProperty("organizations").EnumerateArray()
            .Select(x => x.GetProperty("id").GetGuid())
            .ToList();
}
