using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using GisDashboard.Infrastructure.Persistence;

namespace GisDashboard.Tests;

public sealed class DirectoryAssignmentTests : IClassFixture<ApiFactory>
{
    private readonly ApiFactory _factory;

    public DirectoryAssignmentTests(ApiFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task Assigning_staff_to_org_on_users_adds_assigned_tech_and_member()
    {
        var ga = await _factory.LoginAsync("admin@bisconsultants.local");
        (await ga.PutAsJsonAsync($"/api/admin/users/{SeedIds.EditorOther}", new
        {
            displayName = "Casey Nguyen",
            email = "editor.other@bisconsultants.local",
            role = "Editor",
            organizationIds = new[] { SeedIds.OtherClient, SeedIds.DemoClient },
            isActive = true
        })).StatusCode.Should().Be(HttpStatusCode.OK);

        var orgs = await (await ga.GetAsync("/api/admin/organizations")).ReadJsonAsync();
        var demo = orgs.EnumerateArray().Single(x => x.GetProperty("id").GetGuid() == SeedIds.DemoClient);
        demo.GetProperty("assignedTechs").EnumerateArray()
            .Should().Contain(x => x.GetProperty("id").GetGuid() == SeedIds.EditorOther);
        demo.GetProperty("members").EnumerateArray()
            .Should().Contain(x => x.GetProperty("id").GetGuid() == SeedIds.EditorOther);

        var users = await (await ga.GetAsync("/api/admin/users")).ReadJsonAsync();
        users.EnumerateArray()
            .Single(x => x.GetProperty("id").GetGuid() == SeedIds.EditorOther)
            .GetProperty("organizations")
            .EnumerateArray()
            .Should().Contain(x => x.GetProperty("organizationId").GetGuid() == SeedIds.DemoClient);

        (await ga.PutAsJsonAsync($"/api/admin/users/{SeedIds.EditorOther}", new
        {
            displayName = "Casey Nguyen",
            email = "editor.other@bisconsultants.local",
            role = "Editor",
            organizationIds = new[] { SeedIds.OtherClient },
            isActive = true
        })).StatusCode.Should().Be(HttpStatusCode.OK);

        var after = await (await ga.GetAsync("/api/admin/organizations")).ReadJsonAsync();
        after.EnumerateArray()
            .Single(x => x.GetProperty("id").GetGuid() == SeedIds.DemoClient)
            .GetProperty("assignedTechs")
            .EnumerateArray()
            .Should().NotContain(x => x.GetProperty("id").GetGuid() == SeedIds.EditorOther);
    }

    [Fact]
    public async Task Assigning_tech_on_org_adds_user_membership()
    {
        var ga = await _factory.LoginAsync("admin@bisconsultants.local");
        (await ga.PutAsJsonAsync($"/api/admin/organizations/{SeedIds.OtherClient}", new
        {
            name = "Other Client",
            code = "OTHERCLIENT",
            isActive = true,
            assignedTechIds = new[] { SeedIds.EditorOther, SeedIds.EditorDemo }
        })).StatusCode.Should().Be(HttpStatusCode.OK);

        var users = await (await ga.GetAsync("/api/admin/users")).ReadJsonAsync();
        users.EnumerateArray()
            .Single(x => x.GetProperty("id").GetGuid() == SeedIds.EditorDemo)
            .GetProperty("organizations")
            .EnumerateArray()
            .Should().Contain(x => x.GetProperty("organizationId").GetGuid() == SeedIds.OtherClient);

        await ga.PutAsJsonAsync($"/api/admin/organizations/{SeedIds.OtherClient}", new
        {
            name = "Other Client",
            code = "OTHERCLIENT",
            isActive = true,
            assignedTechIds = new[] { SeedIds.EditorOther }
        });
        await ga.PutAsJsonAsync($"/api/admin/users/{SeedIds.EditorDemo}", new
        {
            displayName = "Alex Rivera",
            email = "editor@bisconsultants.local",
            role = "Editor",
            organizationIds = new[] { SeedIds.DemoClient },
            isActive = true
        });
    }

    [Fact]
    public async Task Viewer_org_assignment_shows_in_members_not_assigned_techs()
    {
        var ga = await _factory.LoginAsync("admin@bisconsultants.local");
        var orgs = await (await ga.GetAsync("/api/admin/organizations")).ReadJsonAsync();
        var demo = orgs.EnumerateArray().Single(x => x.GetProperty("id").GetGuid() == SeedIds.DemoClient);
        demo.GetProperty("members").EnumerateArray()
            .Should().Contain(x => x.GetProperty("id").GetGuid() == SeedIds.ViewerDemo);
        demo.GetProperty("assignedTechs").EnumerateArray()
            .Should().NotContain(x => x.GetProperty("id").GetGuid() == SeedIds.ViewerDemo);
        demo.GetProperty("members").EnumerateArray()
            .Should().Contain(x => x.GetProperty("id").GetGuid() == SeedIds.UploaderDemo);
        demo.GetProperty("assignedTechs").EnumerateArray()
            .Should().NotContain(x => x.GetProperty("id").GetGuid() == SeedIds.UploaderDemo);
        demo.GetProperty("assignedTechs").EnumerateArray()
            .Should().Contain(x => x.GetProperty("id").GetGuid() == SeedIds.OrgAdminDemo);
    }

    [Fact]
    public async Task Seed_backfill_promotes_staff_members_to_assigned_techs()
    {
        var ga = await _factory.LoginAsync("admin@bisconsultants.local");
        var orgs = await (await ga.GetAsync("/api/admin/organizations")).ReadJsonAsync();
        var demo = orgs.EnumerateArray().Single(x => x.GetProperty("id").GetGuid() == SeedIds.DemoClient);
        demo.GetProperty("members").EnumerateArray().Select(x => x.GetProperty("id").GetGuid())
            .Should().BeEquivalentTo(new[] { SeedIds.EditorDemo, SeedIds.ViewerDemo, SeedIds.UploaderDemo, SeedIds.OrgAdminDemo });
        demo.GetProperty("assignedTechs").EnumerateArray().Select(x => x.GetProperty("id").GetGuid())
            .Should().BeEquivalentTo(new[] { SeedIds.EditorDemo, SeedIds.OrgAdminDemo });
    }
}
