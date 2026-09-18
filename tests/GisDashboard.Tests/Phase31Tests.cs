using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using FluentAssertions;
using GisDashboard.Infrastructure.Persistence;

namespace GisDashboard.Tests;

public sealed class Phase31Tests : IClassFixture<ApiFactory>
{
    private readonly ApiFactory _factory;

    public Phase31Tests(ApiFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task Work_items_all_bucket_honors_status_and_day_filters()
    {
        var client = await _factory.LoginAsync("admin@bisconsultants.local");
        var active = await (await client.GetAsync($"/api/work-items?pageSize=100&statusId={SeedIds.StatusInProgress}")).ReadJsonAsync();
        active.GetProperty("items").EnumerateArray().Should().OnlyContain(x =>
            x.GetProperty("statusId").GetGuid() == SeedIds.StatusInProgress);

        var completed = await (await client.GetAsync(
            $"/api/work-items?bucket=completed&pageSize=100&workedFrom=2020-01-01T00:00:00Z")).ReadJsonAsync();
        completed.GetProperty("items").EnumerateArray().Should().OnlyContain(x =>
            x.GetProperty("statusName").GetString() == "Complete" || x.GetProperty("statusName").GetString() == "QC'd");
    }

    [Fact]
    public async Task Dashboard_includes_chart_series_scoped_to_org()
    {
        var global = await _factory.LoginAsync("admin@bisconsultants.local");
        var json = await (await global.GetAsync("/api/dashboard")).ReadJsonAsync();
        json.GetProperty("volumeOverTime").GetArrayLength().Should().Be(30);
        json.GetProperty("hoursByAssignee").GetArrayLength().Should().BeGreaterThan(0);
        json.GetProperty("hoursByClient").EnumerateArray()
            .Select(x => x.GetProperty("name").GetString())
            .Should().Contain("Demo Client");

        var orgAdmin = await _factory.LoginAsync("admin@democlient.local");
        var scoped = await (await orgAdmin.GetAsync("/api/dashboard")).ReadJsonAsync();
        scoped.GetProperty("hoursByClient").EnumerateArray()
            .Select(x => x.GetProperty("name").GetString())
            .Should().Equal("Demo Client");
    }

    [Fact]
    public async Task Global_admin_can_edit_organization()
    {
        var client = await _factory.LoginAsync("admin@bisconsultants.local");
        var response = await client.PutAsJsonAsync($"/api/admin/organizations/{SeedIds.OtherClient}", new
        {
            name = "Other Client LLC",
            code = "OTHERCLIENT",
            isActive = true
        });
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        (await response.ReadJsonAsync()).GetProperty("name").GetString().Should().Be("Other Client LLC");

        await client.PutAsJsonAsync($"/api/admin/organizations/{SeedIds.OtherClient}", new
        {
            name = "Other Client",
            code = "OTHERCLIENT",
            isActive = true
        });
    }

    [Fact]
    public async Task Org_admin_can_save_own_and_other_org_without_changing_code()
    {
        var client = await _factory.LoginAsync("admin@democlient.local");
        var own = await client.PutAsJsonAsync($"/api/admin/organizations/{SeedIds.DemoClient}", new
        {
            name = "Demo Client",
            code = "HACKED",
            isActive = false
        });
        own.StatusCode.Should().Be(HttpStatusCode.OK);
        var json = await own.ReadJsonAsync();
        json.GetProperty("name").GetString().Should().Be("Demo Client");
        json.GetProperty("code").GetString().Should().Be("DEMOCLIENT");
        json.GetProperty("isActive").GetBoolean().Should().BeTrue();

        var other = await client.PutAsJsonAsync($"/api/admin/organizations/{SeedIds.OtherClient}", new
        {
            name = "Other Client",
            code = "OTHERCLIENT",
            isActive = true
        });
        other.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task Editor_cannot_edit_unrelated_org_fields_or_user()
    {
        var client = await _factory.LoginAsync("editor@bisconsultants.local");
        var response = await client.PutAsJsonAsync($"/api/admin/organizations/{SeedIds.DemoClient}", new { name = "X" });
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        (await response.ReadJsonAsync()).GetProperty("name").GetString().Should().Be("Demo Client");
        (await client.PutAsJsonAsync($"/api/admin/users/{SeedIds.ViewerDemo}", new
        {
            displayName = "X",
            email = "viewer@bisconsultants.local",
            role = "Viewer",
            organizationIds = new[] { SeedIds.DemoClient },
            isActive = true
        })).StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task Global_admin_can_edit_user_display_name()
    {
        var client = await _factory.LoginAsync("admin@bisconsultants.local");
        var response = await client.PutAsJsonAsync($"/api/admin/users/{SeedIds.EditorDemo}", new
        {
            displayName = "arivera",
            fullName = "Alex Rivera",
            email = "editor@bisconsultants.local",
            role = "Editor",
            organizationIds = new[] { SeedIds.DemoClient },
            isActive = true
        });
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var edited = await response.ReadJsonAsync();
        edited.GetProperty("displayName").GetString().Should().Be("arivera");
        edited.GetProperty("fullName").GetString().Should().Be("Alex Rivera");
    }

    [Fact]
    public async Task Org_admin_can_edit_other_org_user_but_cannot_promote_global()
    {
        var client = await _factory.LoginAsync("admin@democlient.local");
        (await client.PutAsJsonAsync($"/api/admin/users/{SeedIds.EditorOther}", new
        {
            displayName = "Casey Nguyen",
            email = "editor.other@bisconsultants.local",
            role = "Editor",
            organizationIds = new[] { SeedIds.OtherClient },
            isActive = true
        })).StatusCode.Should().Be(HttpStatusCode.OK);

        (await client.PutAsJsonAsync($"/api/admin/users/{SeedIds.EditorDemo}", new
        {
            displayName = "Alex Rivera",
            email = "editor@bisconsultants.local",
            role = "GlobalAdministrator",
            organizationIds = Array.Empty<Guid>(),
            isActive = true
        })).StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task Client_role_cannot_be_assigned_on_edit()
    {
        var client = await _factory.LoginAsync("admin@bisconsultants.local");
        var response = await client.PutAsJsonAsync($"/api/admin/users/{SeedIds.ViewerDemo}", new
        {
            displayName = "Riley Chen",
            email = "viewer@bisconsultants.local",
            role = "Client",
            organizationIds = new[] { SeedIds.DemoClient },
            isActive = true
        });
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Token_upload_happy_path_creates_pending_item_in_that_org()
    {
        var admin = await _factory.LoginAsync("admin@bisconsultants.local");
        var link = await (await admin.GetAsync($"/api/admin/organizations/{SeedIds.DemoClient}/upload-link")).ReadJsonAsync();
        var token = link.GetProperty("token").GetString();
        token.Should().NotBeNullOrWhiteSpace();
        link.GetProperty("path").GetString().Should().StartWith("/upload/");

        var anon = _factory.CreateClient();
        var info = await anon.GetAsync($"/api/public/uploads/{token}");
        info.StatusCode.Should().Be(HttpStatusCode.OK);
        var page = await info.ReadJsonAsync();
        page.GetProperty("organizationName").GetString().Should().Be("Demo Client");
        page.TryGetProperty("organizationId", out _).Should().BeFalse();

        var uploaded = await PostPublicAsync(anon, token!, "token-plat.pdf");
        uploaded.StatusCode.Should().Be(HttpStatusCode.OK, await uploaded.Content.ReadAsStringAsync());
        var result = await uploaded.ReadJsonAsync();
        result.GetProperty("organizationName").GetString().Should().Be("Demo Client");
        result.GetProperty("statusName").GetString().Should().Be("Pending");
        result.GetProperty("fileName").GetString().Should().Be("token-plat.pdf");

        var id = result.GetProperty("id").GetGuid();
        var detail = await (await admin.GetAsync($"/api/work-items/{id}")).ReadJsonAsync();
        detail.GetProperty("organizationId").GetGuid().Should().Be(SeedIds.DemoClient);
        detail.GetProperty("uploadedByName").GetString().Should().Be("Token upload");
    }

    [Fact]
    public async Task Bad_or_empty_token_is_not_found()
    {
        var anon = _factory.CreateClient();
        (await anon.GetAsync("/api/public/uploads/not-a-real-token-value")).StatusCode.Should().Be(HttpStatusCode.NotFound);
        (await anon.GetAsync("/api/public/uploads/ ")).StatusCode.Should().Be(HttpStatusCode.NotFound);
        (await PostPublicAsync(anon, "zzzz-invalid-token", "nope.pdf")).StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task Token_cannot_upload_into_another_org_and_does_not_open_admin_apis()
    {
        var admin = await _factory.LoginAsync("admin@bisconsultants.local");
        var demo = await (await admin.GetAsync($"/api/admin/organizations/{SeedIds.DemoClient}/upload-link")).ReadJsonAsync();
        var other = await (await admin.GetAsync($"/api/admin/organizations/{SeedIds.OtherClient}/upload-link")).ReadJsonAsync();
        var demoToken = demo.GetProperty("token").GetString()!;
        var otherToken = other.GetProperty("token").GetString()!;
        demoToken.Should().NotBe(otherToken);

        var anon = _factory.CreateClient();
        var uploaded = await PostPublicAsync(anon, demoToken, "stay-in-demo.pdf");
        uploaded.StatusCode.Should().Be(HttpStatusCode.OK);
        var id = (await uploaded.ReadJsonAsync()).GetProperty("id").GetGuid();
        var detail = await (await admin.GetAsync($"/api/work-items/{id}")).ReadJsonAsync();
        detail.GetProperty("organizationName").GetString().Should().Be("Demo Client");

        (await anon.GetAsync("/api/admin/organizations")).StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        (await anon.GetAsync("/api/work-items")).StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        (await anon.GetAsync($"/api/work-items/{id}")).StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Regenerating_token_invalidates_the_old_link()
    {
        var admin = await _factory.LoginAsync("admin@bisconsultants.local");
        var first = await (await admin.GetAsync($"/api/admin/organizations/{SeedIds.OtherClient}/upload-link")).ReadJsonAsync();
        var oldToken = first.GetProperty("token").GetString()!;

        var regen = await admin.PostAsync($"/api/admin/organizations/{SeedIds.OtherClient}/upload-link/regenerate", null);
        regen.StatusCode.Should().Be(HttpStatusCode.OK);
        var next = (await regen.ReadJsonAsync()).GetProperty("token").GetString()!;
        next.Should().NotBe(oldToken);

        var anon = _factory.CreateClient();
        (await anon.GetAsync($"/api/public/uploads/{oldToken}")).StatusCode.Should().Be(HttpStatusCode.NotFound);
        (await anon.GetAsync($"/api/public/uploads/{next}")).StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task Org_admin_can_read_other_org_upload_link()
    {
        var client = await _factory.LoginAsync("admin@democlient.local");
        (await client.GetAsync($"/api/admin/organizations/{SeedIds.OtherClient}/upload-link"))
            .StatusCode.Should().Be(HttpStatusCode.OK);

        var own = await client.GetAsync($"/api/admin/organizations/{SeedIds.DemoClient}/upload-link");
        own.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task Settings_phase_is_3_1_and_token_upload_is_enabled()
    {
        var client = await _factory.LoginAsync("admin@bisconsultants.local");
        var json = await (await client.GetAsync("/api/settings")).ReadJsonAsync();
        json.GetProperty("phase").GetString().Should().Be("Phase 3.1");
        json.GetProperty("features").GetProperty("tokenizedUpload").GetProperty("enabled").GetBoolean().Should().BeTrue();
    }

    private static async Task<HttpResponseMessage> PostPublicAsync(HttpClient client, string token, string fileName)
    {
        using var form = new MultipartFormDataContent();
        form.Add(new StringContent(SeedIds.TypeDeed.ToString()), "documentTypeId");
        form.Add(new StringContent("Deed"), "documentTypeName");
        var file = new ByteArrayContent([0x25, 0x50, 0x44, 0x46, 0x2D]);
        file.Headers.ContentType = new MediaTypeHeaderValue("application/pdf");
        form.Add(file, "file", fileName);
        return await client.PostAsync($"/api/public/uploads/{token}", form);
    }
}
