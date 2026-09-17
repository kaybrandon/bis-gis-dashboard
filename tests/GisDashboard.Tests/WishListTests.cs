using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using FluentAssertions;
using GisDashboard.Infrastructure.Persistence;

namespace GisDashboard.Tests;

public sealed class WishListTests : IClassFixture<ApiFactory>
{
    private readonly ApiFactory _factory;

    public WishListTests(ApiFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task Staff_see_every_organization_viewer_does_not()
    {
        var editor = await _factory.LoginAsync("editor@bisconsultants.local");
        var me = await (await editor.GetAsync("/api/auth/me")).ReadJsonAsync();
        me.GetProperty("canSeeAllOrganizations").GetBoolean().Should().BeTrue();
        me.GetProperty("canUpload").GetBoolean().Should().BeTrue();
        (await editor.GetAsync($"/api/work-items/{SeedIds.OtherPlat}")).StatusCode.Should().Be(HttpStatusCode.OK);

        var orgAdmin = await _factory.LoginAsync("admin@democlient.local");
        var adminMe = await (await orgAdmin.GetAsync("/api/auth/me")).ReadJsonAsync();
        adminMe.GetProperty("canSeeAllOrganizations").GetBoolean().Should().BeTrue();
        (await orgAdmin.GetAsync($"/api/work-items/{SeedIds.OtherPlat}")).StatusCode.Should().Be(HttpStatusCode.OK);

        var viewer = await _factory.LoginAsync("viewer@bisconsultants.local");
        var viewerMe = await (await viewer.GetAsync("/api/auth/me")).ReadJsonAsync();
        viewerMe.GetProperty("canSeeAllOrganizations").GetBoolean().Should().BeFalse();
        viewerMe.GetProperty("canUpload").GetBoolean().Should().BeTrue();
        (await viewer.GetAsync($"/api/work-items/{SeedIds.OtherPlat}")).StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task Viewer_can_upload_to_own_org_and_not_the_other()
    {
        var viewer = await _factory.LoginAsync("viewer@bisconsultants.local");
        var own = await UploadAsync(viewer, SeedIds.DemoClient.ToString(), "Demo Client", "", "", "viewer-supply.pdf");
        own.StatusCode.Should().Be(HttpStatusCode.OK, await own.Content.ReadAsStringAsync());
        var created = await own.ReadJsonAsync();
        created.GetProperty("organizationName").GetString().Should().Be("Demo Client");
        created.GetProperty("documentTypeName").GetString().Should().Be("Other");
        created.GetProperty("assignedToName").GetString().Should().Be("Alex Rivera");
        created.GetProperty("isPriority").GetBoolean().Should().BeFalse();
        created.GetProperty("canMutate").GetBoolean().Should().BeFalse();

        var other = await UploadAsync(viewer, SeedIds.OtherClient.ToString(), "Other Client", "", "", "viewer-cross.pdf");
        other.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task Viewer_cannot_set_reviewed_or_cancelled()
    {
        var viewer = await _factory.LoginAsync("viewer@bisconsultants.local");
        (await viewer.PatchAsJsonAsync($"/api/work-items/{SeedIds.DemoPlat}", new
        {
            isReviewed = true,
            statusId = SeedIds.StatusCancelled
        })).StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task Editor_can_mark_reviewed_and_cancelled()
    {
        var editor = await _factory.LoginAsync("editor@bisconsultants.local");
        var response = await editor.PatchAsJsonAsync($"/api/work-items/{SeedIds.DemoSurvey}", new
        {
            isReviewed = true,
            statusId = SeedIds.StatusCancelled
        });
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var json = await response.ReadJsonAsync();
        json.GetProperty("isReviewed").GetBoolean().Should().BeTrue();
        json.GetProperty("statusId").GetGuid().Should().Be(SeedIds.StatusCancelled);
        json.GetProperty("statusName").GetString().Should().Be("Cancelled");
    }

    [Fact]
    public async Task Document_types_are_deed_plat_survey_subdivision_other()
    {
        var client = await _factory.LoginAsync("admin@bisconsultants.local");
        var types = await (await client.GetAsync("/api/lookups/document-types")).ReadJsonAsync();
        types.EnumerateArray().Select(x => x.GetProperty("name").GetString()).Should().Equal(
            "Deed", "Plat", "Survey", "Subdivision", "Other");
    }

    [Fact]
    public async Task Status_actions_include_cancelled()
    {
        var client = await _factory.LoginAsync("admin@bisconsultants.local");
        var actions = await (await client.GetAsync("/api/lookups/status-actions")).ReadJsonAsync();
        actions.GetProperty("cancelledId").GetGuid().Should().Be(SeedIds.StatusCancelled);
        var statuses = await (await client.GetAsync("/api/lookups/statuses")).ReadJsonAsync();
        statuses.EnumerateArray().Select(x => x.GetProperty("name").GetString()).Should().Contain("Cancelled");
    }

    [Fact]
    public async Task Upload_auto_assigns_org_bis_editor()
    {
        var admin = await _factory.LoginAsync("admin@bisconsultants.local");
        var response = await UploadAsync(admin, SeedIds.DemoClient.ToString(), "Demo Client", SeedIds.TypeDeed.ToString(), "Deed", "auto-assign.pdf");
        response.StatusCode.Should().Be(HttpStatusCode.OK, await response.Content.ReadAsStringAsync());
        var json = await response.ReadJsonAsync();
        json.GetProperty("assignedToName").GetString().Should().Be("Alex Rivera");
        json.GetProperty("isReviewed").GetBoolean().Should().BeFalse();
    }

    [Fact]
    public async Task List_includes_reviewed_flag()
    {
        var editor = await _factory.LoginAsync("editor@bisconsultants.local");
        await editor.PatchAsJsonAsync($"/api/work-items/{SeedIds.DemoPlat}", new { isReviewed = true });
        var list = await (await editor.GetAsync("/api/work-items?pageSize=100")).ReadJsonAsync();
        var plat = list.GetProperty("items").EnumerateArray()
            .First(x => x.GetProperty("id").GetGuid() == SeedIds.DemoPlat);
        plat.GetProperty("isReviewed").GetBoolean().Should().BeTrue();
    }

    private static async Task<HttpResponseMessage> UploadAsync(
        HttpClient client,
        string organizationId,
        string organizationName,
        string documentTypeId,
        string documentTypeName,
        string fileName)
    {
        using var form = new MultipartFormDataContent();
        if (!string.IsNullOrEmpty(organizationId))
        {
            form.Add(new StringContent(organizationId), "organizationId");
        }

        if (!string.IsNullOrEmpty(organizationName))
        {
            form.Add(new StringContent(organizationName), "organizationName");
        }

        if (!string.IsNullOrEmpty(documentTypeId))
        {
            form.Add(new StringContent(documentTypeId), "documentTypeId");
        }

        if (!string.IsNullOrEmpty(documentTypeName))
        {
            form.Add(new StringContent(documentTypeName), "documentTypeName");
        }

        var file = new ByteArrayContent([0x25, 0x50, 0x44, 0x46, 0x2D]);
        file.Headers.ContentType = new MediaTypeHeaderValue("application/pdf");
        form.Add(file, "file", fileName);
        return await client.PostAsync("/api/work-items", form);
    }
}
