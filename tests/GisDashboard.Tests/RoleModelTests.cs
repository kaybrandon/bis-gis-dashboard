using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using GisDashboard.Infrastructure.Persistence;

namespace GisDashboard.Tests;

public sealed class RoleModelTests : IClassFixture<ApiFactory>
{
    private readonly ApiFactory _factory;

    public RoleModelTests(ApiFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task Org_admin_user_list_includes_staff_across_clients()
    {
        var client = await _factory.LoginAsync("admin@democlient.local");
        var response = await client.GetAsync("/api/admin/users");
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var json = await response.ReadJsonAsync();
        var emails = json.EnumerateArray().Select(x => x.GetProperty("email").GetString()).ToList();
        emails.Should().Contain("admin@democlient.local");
        emails.Should().Contain("editor@bisconsultants.local");
        emails.Should().Contain("viewer@bisconsultants.local");
        emails.Should().Contain("admin@otherclient.local");
        emails.Should().Contain("editor.other@bisconsultants.local");
        emails.Should().NotContain("admin@bisconsultants.local");
    }

    [Fact]
    public async Task Org_admin_org_list_includes_every_client()
    {
        var client = await _factory.LoginAsync("admin@democlient.local");
        var json = await (await client.GetAsync("/api/admin/organizations")).ReadJsonAsync();
        json.EnumerateArray().Select(x => x.GetProperty("name").GetString())
            .Should().Contain("Demo Client")
            .And.Contain("Other Client");
    }

    [Fact]
    public async Task Org_admin_cannot_create_organization()
    {
        var client = await _factory.LoginAsync("admin@democlient.local");
        var response = await client.PostAsJsonAsync("/api/admin/organizations", new
        {
            name = "Shadow Client",
            code = "SHADOW"
        });
        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task Org_admin_cannot_create_global_administrator()
    {
        var client = await _factory.LoginAsync("admin@democlient.local");
        var response = await client.PostAsJsonAsync("/api/admin/users", new
        {
            email = "rogue@bisconsultants.local",
            password = "Demo!Gis2026",
            displayName = "Rogue",
            role = "GlobalAdministrator",
            organizationIds = Array.Empty<Guid>()
        });
        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task Org_admin_can_assign_other_org_to_new_user()
    {
        var client = await _factory.LoginAsync("admin@democlient.local");
        var response = await client.PostAsJsonAsync("/api/admin/users", new
        {
            email = "cross@democlient.local",
            password = "Demo!Gis2026",
            displayName = "Cross",
            role = "Editor",
            organizationIds = new[] { SeedIds.OtherClient }
        });
        response.StatusCode.Should().Be(HttpStatusCode.OK, await response.Content.ReadAsStringAsync());
    }

    [Fact]
    public async Task Org_admin_can_create_editor_in_own_org()
    {
        var client = await _factory.LoginAsync("admin@democlient.local");
        var response = await client.PostAsJsonAsync("/api/admin/users", new
        {
            email = "new.editor@democlient.local",
            password = "Demo!Gis2026",
            displayName = "New Editor",
            role = "Editor",
            organizationIds = new[] { SeedIds.DemoClient }
        });
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var json = await response.ReadJsonAsync();
        json.GetProperty("role").GetString().Should().Be("Editor");
        json.GetProperty("organizations")[0].GetProperty("organizationName").GetString().Should().Be("Demo Client");
    }

    [Fact]
    public async Task Uploader_role_is_assignable()
    {
        var client = await _factory.LoginAsync("admin@bisconsultants.local");
        var response = await client.PostAsJsonAsync("/api/admin/users", new
        {
            email = "rolemodel.uploader@democlient.local",
            password = "Demo!Gis2026",
            displayName = "Lee Brooks",
            role = "Uploader",
            organizationIds = new[] { SeedIds.DemoClient }
        });
        response.StatusCode.Should().Be(HttpStatusCode.OK, await response.Content.ReadAsStringAsync());
        (await response.ReadJsonAsync()).GetProperty("role").GetString().Should().Be("Uploader");
    }

    [Fact]
    public async Task Client_role_is_not_assignable()
    {
        var client = await _factory.LoginAsync("admin@bisconsultants.local");
        var response = await client.PostAsJsonAsync("/api/admin/users", new
        {
            email = "legacy@democlient.local",
            password = "Demo!Gis2026",
            displayName = "Legacy",
            role = "Client",
            organizationIds = new[] { SeedIds.DemoClient }
        });
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task No_seed_user_has_client_role()
    {
        var client = await _factory.LoginAsync("admin@bisconsultants.local");
        var json = await (await client.GetAsync("/api/admin/users")).ReadJsonAsync();
        json.EnumerateArray().Select(x => x.GetProperty("role").GetString())
            .Should().NotContain("Client");
        json.EnumerateArray().Select(x => x.GetProperty("email").GetString())
            .Should().NotContain(email => email != null && email.StartsWith("client@", StringComparison.Ordinal));
    }
}
