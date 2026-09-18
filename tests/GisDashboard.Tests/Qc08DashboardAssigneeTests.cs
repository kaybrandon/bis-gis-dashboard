using System.Net;
using System.Text.Json;
using FluentAssertions;
using GisDashboard.Domain;
using GisDashboard.Infrastructure.Persistence;

namespace GisDashboard.Tests;

/// <summary>
/// QC08 — Hide Dashboard Assignee for Viewer/Uploader and isolate assigned-org data.
/// Hidden control is not isolation. Editors/Administrators keep authorized cross-org filtering (QC03).
/// </summary>
public sealed class Qc08DashboardAssigneeTests : IClassFixture<ApiFactory>
{
    private readonly ApiFactory _factory;

    public Qc08DashboardAssigneeTests(ApiFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public void Assignee_filter_is_hidden_for_viewer_and_uploader_only()
    {
        Roles.CanSeeDashboardAssignee(Roles.Viewer).Should().BeFalse();
        Roles.CanSeeDashboardAssignee(Roles.Uploader).Should().BeFalse();
        Roles.CanSeeDashboardAssignee(Roles.Editor).Should().BeTrue();
        Roles.CanSeeDashboardAssignee(Roles.Administrator).Should().BeTrue();
        Roles.CanSeeDashboardAssignee(Roles.GlobalAdministrator).Should().BeTrue();
    }

    [Fact]
    public async Task Me_hides_dashboard_assignee_for_viewer_and_uploader()
    {
        var viewer = await (await (await _factory.LoginAsync("viewer@bisconsultants.local"))
            .GetAsync("/api/auth/me")).ReadJsonAsync();
        viewer.GetProperty("canSeeDashboardAssignee").GetBoolean().Should().BeFalse();
        viewer.GetProperty("canSeeAllOrganizations").GetBoolean().Should().BeFalse();

        var uploader = await (await (await _factory.LoginAsync("uploader@bisconsultants.local"))
            .GetAsync("/api/auth/me")).ReadJsonAsync();
        uploader.GetProperty("canSeeDashboardAssignee").GetBoolean().Should().BeFalse();
        uploader.GetProperty("canSeeAllOrganizations").GetBoolean().Should().BeFalse();
    }

    [Fact]
    public async Task Me_keeps_dashboard_assignee_for_editor_and_administrators()
    {
        foreach (var email in new[]
        {
            "editor@bisconsultants.local",
            "admin@democlient.local",
            "admin@bisconsultants.local"
        })
        {
            var me = await (await (await _factory.LoginAsync(email)).GetAsync("/api/auth/me")).ReadJsonAsync();
            me.GetProperty("canSeeDashboardAssignee").GetBoolean().Should().BeTrue();
            me.GetProperty("canSeeAllOrganizations").GetBoolean().Should().BeTrue();
        }
    }

    [Theory]
    [InlineData("viewer@bisconsultants.local")]
    [InlineData("uploader@bisconsultants.local")]
    public async Task Client_role_dashboard_exposes_only_assigned_org(string email)
    {
        var client = await _factory.LoginAsync(email);
        var orgs = await (await client.GetAsync("/api/lookups/organizations")).ReadJsonAsync();
        orgs.EnumerateArray().Select(x => x.GetProperty("name").GetString())
            .Should().Equal("Demo Client");

        var assignees = await client.GetAsync("/api/lookups/assignees");
        assignees.StatusCode.Should().Be(HttpStatusCode.OK);
        (await assignees.ReadJsonAsync()).GetArrayLength().Should().Be(0);

        var dash = await (await client.GetAsync("/api/dashboard?from=2026-07-01&to=2026-09-18")).ReadJsonAsync();
        AssertAssignedOrgOnly(dash);

        var items = await (await client.GetAsync("/api/work-items?pageSize=100")).ReadJsonAsync();
        items.GetProperty("items").EnumerateArray()
            .Select(x => x.GetProperty("organizationName").GetString())
            .Should().OnlyContain(name => name == "Demo Client");
    }

    [Theory]
    [InlineData("viewer@bisconsultants.local")]
    [InlineData("uploader@bisconsultants.local")]
    public async Task Client_role_cannot_bypass_org_isolation_via_filters_or_api(string email)
    {
        var client = await _factory.LoginAsync(email);

        (await client.GetAsync($"/api/dashboard?organizationId={SeedIds.OtherClient}"))
            .StatusCode.Should().Be(HttpStatusCode.NotFound);
        (await client.GetAsync($"/api/dashboard/pdf?organizationId={SeedIds.OtherClient}"))
            .StatusCode.Should().Be(HttpStatusCode.NotFound);
        (await client.GetAsync($"/api/work-items?organizationId={SeedIds.OtherClient}&pageSize=100"))
            .StatusCode.Should().Be(HttpStatusCode.NotFound);
        (await client.GetAsync($"/api/work-items/{SeedIds.OtherPlat}"))
            .StatusCode.Should().Be(HttpStatusCode.NotFound);
        (await client.GetAsync($"/api/lookups/assigned-technicians?organizationId={SeedIds.OtherClient}"))
            .StatusCode.Should().Be(HttpStatusCode.NotFound);

        var bypass = await (await client.GetAsync(
            $"/api/dashboard?from=2026-07-01&to=2026-09-18&assignedToUserId={SeedIds.EditorOther}"))
            .ReadJsonAsync();
        AssertAssignedOrgOnly(bypass);
        bypass.GetProperty("kpis").EnumerateArray().Sum(x => x.GetProperty("count").GetInt32())
            .Should().BeGreaterThan(0, "assignee query param is dropped, not used to hide assigned-org rows");
    }

    [Fact]
    public async Task Editor_and_admin_retain_cross_org_dashboard_filters()
    {
        foreach (var email in new[] { "editor@bisconsultants.local", "admin@democlient.local" })
        {
            var client = await _factory.LoginAsync(email);
            var me = await (await client.GetAsync("/api/auth/me")).ReadJsonAsync();
            me.GetProperty("canSeeDashboardAssignee").GetBoolean().Should().BeTrue();

            var assignees = await (await client.GetAsync("/api/lookups/assignees")).ReadJsonAsync();
            assignees.EnumerateArray().Select(x => x.GetProperty("id").GetGuid())
                .Should().Contain(SeedIds.EditorDemo)
                .And.Contain(SeedIds.EditorOther);

            var all = await (await client.GetAsync("/api/dashboard?from=2026-07-01&to=2026-09-18")).ReadJsonAsync();
            OrgNames(all.GetProperty("organizationCounts")).Should().Contain(["Demo Client", "Other Client"]);
            ClientNames(all.GetProperty("hoursByClient")).Should().Contain("Demo Client");

            var other = await client.GetAsync(
                $"/api/dashboard?from=2026-07-01&to=2026-09-18&organizationId={SeedIds.OtherClient}");
            other.StatusCode.Should().Be(HttpStatusCode.OK, await other.Content.ReadAsStringAsync());
            var otherDash = await other.ReadJsonAsync();
            OrgNames(otherDash.GetProperty("organizationCounts")).Should().Equal("Other Client");
            otherDash.GetProperty("recentCompleted").EnumerateArray()
                .Select(x => x.GetProperty("organizationName").GetString())
                .Should().OnlyContain(name => name == "Other Client");

            var byAssignee = await (await client.GetAsync(
                $"/api/dashboard?from=2026-07-01&to=2026-09-18&assignedToUserId={SeedIds.EditorOther}"))
                .ReadJsonAsync();
            byAssignee.GetProperty("recentCompleted").EnumerateArray()
                .Select(x => x.GetProperty("assignedToUserId").GetGuid())
                .Should().OnlyContain(id => id == SeedIds.EditorOther);
            OrgNames(byAssignee.GetProperty("organizationCounts")).Should().Contain("Other Client");

            var items = await (await client.GetAsync(
                $"/api/work-items?organizationId={SeedIds.OtherClient}&pageSize=100")).ReadJsonAsync();
            items.GetProperty("items").EnumerateArray()
                .Select(x => x.GetProperty("organizationName").GetString())
                .Should().OnlyContain(name => name == "Other Client")
                .And.NotBeEmpty();
        }
    }

    [Fact]
    public async Task Settings_describe_qc08_dashboard_assignee_isolation()
    {
        var client = await _factory.LoginAsync("admin@bisconsultants.local");
        var settings = await (await client.GetAsync("/api/settings")).ReadJsonAsync();
        var note = settings.GetProperty("features").GetProperty("dashboardAssignee").GetProperty("note").GetString();
        note.Should().Contain("QC08");
        note.Should().Contain("Viewer");
        note.Should().Contain("Uploader");
        note.Should().Contain("QC03");
    }

    private static void AssertAssignedOrgOnly(JsonElement dash)
    {
        OrgNames(dash.GetProperty("organizationCounts")).Should().Equal("Demo Client");
        ClientNames(dash.GetProperty("hoursByClient")).Should().OnlyContain(name => name == "Demo Client");
        dash.GetProperty("recentCompleted").EnumerateArray()
            .Select(x => x.GetProperty("organizationName").GetString())
            .Should().OnlyContain(name => name == "Demo Client");
        dash.GetProperty("assigneeCounts").EnumerateArray()
            .Select(x => x.GetProperty("id").GetGuid())
            .Should().NotContain(SeedIds.EditorOther);
    }

    private static IReadOnlyList<string?> OrgNames(JsonElement counts) =>
        counts.EnumerateArray().Select(x => x.GetProperty("name").GetString()).ToList();

    private static IReadOnlyList<string?> ClientNames(JsonElement hours) =>
        hours.EnumerateArray().Select(x => x.GetProperty("name").GetString()).ToList();
}
