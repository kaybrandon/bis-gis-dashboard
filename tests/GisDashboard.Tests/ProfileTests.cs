using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using GisDashboard.Infrastructure.Persistence;

namespace GisDashboard.Tests;

public sealed class ProfileTests : IClassFixture<ApiFactory>
{
    private readonly ApiFactory _factory;

    public ProfileTests(ApiFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task Viewer_can_update_own_profile_fields()
    {
        var admin = await _factory.LoginAsync("admin@bisconsultants.local");
        (await admin.PostAsJsonAsync("/api/admin/users", new
        {
            email = "profile.viewer@bisconsultants.local",
            password = "Demo!Gis2026",
            displayName = "profileviewer",
            role = "Viewer",
            organizationIds = new[] { SeedIds.DemoClient }
        })).StatusCode.Should().Be(HttpStatusCode.OK);

        var client = await _factory.LoginAsync("profile.viewer@bisconsultants.local");
        var response = await client.PutAsJsonAsync("/api/auth/me", new
        {
            userName = "jordanhale",
            fullName = "Jordan Hale",
            email = "profile.viewer@bisconsultants.local",
            workPhone = "512-555-0100"
        });
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var json = await response.ReadJsonAsync();
        json.GetProperty("displayName").GetString().Should().Be("jordanhale");
        json.GetProperty("userName").GetString().Should().Be("jordanhale");
        json.GetProperty("fullName").GetString().Should().Be("Jordan Hale");
        json.GetProperty("workPhone").GetString().Should().Be("512-555-0100");
    }

    [Fact]
    public async Task Password_change_requires_current_and_matching_confirmation()
    {
        var client = await _factory.LoginAsync("editor@bisconsultants.local");
        (await client.PutAsJsonAsync("/api/auth/me", new
        {
            userName = "Alex Rivera",
            email = "editor@bisconsultants.local",
            newPassword = "New!Gis2026",
            confirmPassword = "New!Gis2026"
        })).StatusCode.Should().Be(HttpStatusCode.BadRequest);

        (await client.PutAsJsonAsync("/api/auth/me", new
        {
            userName = "Alex Rivera",
            email = "editor@bisconsultants.local",
            currentPassword = "Demo!Gis2026",
            newPassword = "New!Gis2026",
            confirmPassword = "Mismatch!Gis"
        })).StatusCode.Should().Be(HttpStatusCode.BadRequest);

        (await client.PutAsJsonAsync("/api/auth/me", new
        {
            userName = "Alex Rivera",
            email = "editor@bisconsultants.local",
            currentPassword = "Wrong!Gis2026",
            newPassword = "New!Gis2026",
            confirmPassword = "New!Gis2026"
        })).StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task Password_change_succeeds_with_current_and_matching_new()
    {
        var admin = await _factory.LoginAsync("admin@bisconsultants.local");
        var created = await admin.PostAsJsonAsync("/api/admin/users", new
        {
            email = "profile.pw@bisconsultants.local",
            password = "Demo!Gis2026",
            displayName = "profilepw",
            role = "Editor",
            organizationIds = new[] { SeedIds.DemoClient }
        });
        created.StatusCode.Should().Be(HttpStatusCode.OK);

        var client = await _factory.LoginAsync("profile.pw@bisconsultants.local");
        var response = await client.PutAsJsonAsync("/api/auth/me", new
        {
            userName = "profilepw",
            email = "profile.pw@bisconsultants.local",
            currentPassword = "Demo!Gis2026",
            newPassword = "Changed!Gis2026",
            confirmPassword = "Changed!Gis2026"
        });
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var login = _factory.CreateClient();
        (await login.PostAsJsonAsync("/api/auth/login", new
        {
            email = "profile.pw@bisconsultants.local",
            password = "Changed!Gis2026"
        })).StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task Profile_cannot_change_role()
    {
        var email = $"profile.role.{Guid.NewGuid():N}@bisconsultants.local";
        var admin = await _factory.LoginAsync("admin@bisconsultants.local");
        (await admin.PostAsJsonAsync("/api/admin/users", new
        {
            email,
            password = "Demo!Gis2026",
            displayName = "profilerole",
            role = "Viewer",
            organizationIds = new[] { SeedIds.DemoClient }
        })).StatusCode.Should().Be(HttpStatusCode.OK);

        var client = await _factory.LoginAsync(email);
        await client.PutAsJsonAsync("/api/auth/me", new
        {
            userName = "profilerole",
            email,
            role = "GlobalAdministrator"
        });
        var me = await (await client.GetAsync("/api/auth/me")).ReadJsonAsync();
        me.GetProperty("role").GetString().Should().Be("Viewer");
    }

    [Fact]
    public async Task Admin_can_set_full_name_and_work_phone_on_user()
    {
        var client = await _factory.LoginAsync("admin@bisconsultants.local");
        var response = await client.PutAsJsonAsync($"/api/admin/users/{SeedIds.EditorDemo}", new
        {
            displayName = "Alex Rivera",
            fullName = "Alex Rivera",
            workPhone = "512-555-0199",
            email = "editor@bisconsultants.local",
            role = "Editor",
            organizationIds = new[] { SeedIds.DemoClient },
            isActive = true
        });
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var json = await response.ReadJsonAsync();
        json.GetProperty("fullName").GetString().Should().Be("Alex Rivera");
        json.GetProperty("workPhone").GetString().Should().Be("512-555-0199");
    }

    [Fact]
    public async Task Avatar_rejects_empty_upload()
    {
        var client = await _factory.LoginAsync("viewer@bisconsultants.local");
        using var content = new MultipartFormDataContent();
        var response = await client.PostAsync("/api/auth/me/avatar", content);
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }
}
