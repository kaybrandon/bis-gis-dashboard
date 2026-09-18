using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using FluentAssertions;
using GisDashboard.Domain;
using GisDashboard.Infrastructure.Persistence;

namespace GisDashboard.Tests;

/// <summary>
/// QC04 — Add Uploader role.
/// Brandon lock 2026-09-17: do not migrate existing Viewers; Viewer stays upload/comment-free.
/// </summary>
public sealed class Qc04UploaderTests : IClassFixture<ApiFactory>
{
    private readonly ApiFactory _factory;

    public Qc04UploaderTests(ApiFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public void Existing_viewers_are_not_migrated_to_uploader()
    {
        Roles.All.Should().Contain(Roles.Uploader);
        Roles.All.Should().Contain(Roles.Viewer);
        Roles.CanUpload(Roles.Viewer).Should().BeFalse();
        Roles.CanPostComments(Roles.Viewer).Should().BeFalse();
        Roles.CanUpload(Roles.Uploader).Should().BeTrue();
        Roles.CanPostComments(Roles.Uploader).Should().BeTrue();
        Roles.CanSeeInternalNotes(Roles.Uploader).Should().BeFalse();
        Roles.CanMutateWorkItems(Roles.Uploader).Should().BeFalse();
        Roles.CanManageDirectory(Roles.Uploader).Should().BeFalse();
        Roles.CanManageAssignedTechs(Roles.Uploader).Should().BeFalse();
        Roles.CanSeeDashboardAssignee(Roles.Uploader).Should().BeFalse();
        Roles.CanSeeDashboardAssignee(Roles.Viewer).Should().BeFalse();
    }

    [Fact]
    public async Task Seed_viewer_stays_viewer_and_uploader_is_a_separate_user()
    {
        var viewer = await _factory.LoginAsync("viewer@bisconsultants.local");
        var viewerMe = await (await viewer.GetAsync("/api/auth/me")).ReadJsonAsync();
        viewerMe.GetProperty("role").GetString().Should().Be("Viewer");
        viewerMe.GetProperty("canUpload").GetBoolean().Should().BeFalse();
        viewerMe.GetProperty("canPostComments").GetBoolean().Should().BeFalse();
        viewerMe.GetProperty("canSeeDashboardAssignee").GetBoolean().Should().BeFalse();

        var uploader = await _factory.LoginAsync("uploader@bisconsultants.local");
        var uploaderMe = await (await uploader.GetAsync("/api/auth/me")).ReadJsonAsync();
        uploaderMe.GetProperty("role").GetString().Should().Be("Uploader");
        uploaderMe.GetProperty("roleDisplayName").GetString().Should().Be("Uploader");
        uploaderMe.GetProperty("email").GetString().Should().Be("uploader@bisconsultants.local");
        uploaderMe.GetProperty("canUpload").GetBoolean().Should().BeTrue();
        uploaderMe.GetProperty("canPostComments").GetBoolean().Should().BeTrue();
        uploaderMe.GetProperty("canSeeInternalNotes").GetBoolean().Should().BeFalse();
        uploaderMe.GetProperty("canEditInternalNotes").GetBoolean().Should().BeFalse();
        uploaderMe.GetProperty("canMutateWorkItems").GetBoolean().Should().BeFalse();
        uploaderMe.GetProperty("canManageDirectory").GetBoolean().Should().BeFalse();
        uploaderMe.GetProperty("canManageAssignedTechs").GetBoolean().Should().BeFalse();
        uploaderMe.GetProperty("canSeeAllOrganizations").GetBoolean().Should().BeFalse();
        uploaderMe.GetProperty("canSeeDashboardAssignee").GetBoolean().Should().BeFalse();
        uploaderMe.GetProperty("organizations").EnumerateArray()
            .Select(x => x.GetProperty("id").GetGuid())
            .Should().Equal(SeedIds.DemoClient);
    }

    [Fact]
    public async Task Admin_can_create_uploader_with_orgs_and_role_persists()
    {
        var admin = await _factory.LoginAsync("admin@bisconsultants.local");
        var created = await admin.PostAsJsonAsync("/api/admin/users", new
        {
            email = "qc04.uploader@democlient.local",
            password = "Demo!Gis2026",
            displayName = "Sam Ortiz",
            role = "Uploader",
            organizationIds = new[] { SeedIds.DemoClient, SeedIds.OtherClient }
        });
        created.StatusCode.Should().Be(HttpStatusCode.OK, await created.Content.ReadAsStringAsync());
        var json = await created.ReadJsonAsync();
        json.GetProperty("role").GetString().Should().Be("Uploader");
        json.GetProperty("organizations").EnumerateArray()
            .Select(x => x.GetProperty("organizationId").GetGuid())
            .Should().BeEquivalentTo(new[] { SeedIds.DemoClient, SeedIds.OtherClient });

        var listed = await (await admin.GetAsync("/api/admin/users")).ReadJsonAsync();
        listed.EnumerateArray().Should().Contain(x =>
            x.GetProperty("email").GetString() == "qc04.uploader@democlient.local"
            && x.GetProperty("role").GetString() == "Uploader");

        var client = await _factory.LoginAsync("qc04.uploader@democlient.local");
        var me = await (await client.GetAsync("/api/auth/me")).ReadJsonAsync();
        me.GetProperty("role").GetString().Should().Be("Uploader");
        me.GetProperty("organizations").GetArrayLength().Should().Be(2);
    }

    [Fact]
    public async Task Org_admin_can_create_uploader_in_assigned_org()
    {
        var admin = await _factory.LoginAsync("admin@democlient.local");
        var response = await admin.PostAsJsonAsync("/api/admin/users", new
        {
            email = "qc04.orgadmin.uploader@democlient.local",
            password = "Demo!Gis2026",
            displayName = "Pat Quinn",
            role = "Uploader",
            organizationIds = new[] { SeedIds.DemoClient }
        });
        response.StatusCode.Should().Be(HttpStatusCode.OK, await response.Content.ReadAsStringAsync());
        (await response.ReadJsonAsync()).GetProperty("role").GetString().Should().Be("Uploader");
    }

    [Fact]
    public async Task Uploader_sees_assigned_org_documents_only()
    {
        var uploader = await _factory.LoginAsync("uploader@bisconsultants.local");
        (await uploader.GetAsync($"/api/work-items/{SeedIds.DemoPlat}")).StatusCode.Should().Be(HttpStatusCode.OK);
        (await uploader.GetAsync($"/api/work-items/{SeedIds.OtherPlat}")).StatusCode.Should().Be(HttpStatusCode.NotFound);

        var list = await (await uploader.GetAsync("/api/work-items?pageSize=100")).ReadJsonAsync();
        list.GetProperty("items").EnumerateArray()
            .Select(x => x.GetProperty("organizationName").GetString())
            .Should().OnlyContain(name => name == "Demo Client");
    }

    [Fact]
    public async Task Uploader_can_upload_to_assigned_org_only()
    {
        var uploader = await _factory.LoginAsync("uploader@bisconsultants.local");
        var own = await UploadAsync(uploader, SeedIds.DemoClient, "Demo Client", "qc04-own.pdf");
        own.StatusCode.Should().Be(HttpStatusCode.OK, await own.Content.ReadAsStringAsync());
        var created = await own.ReadJsonAsync();
        created.GetProperty("organizationName").GetString().Should().Be("Demo Client");
        created.GetProperty("canMutate").GetBoolean().Should().BeFalse();

        var other = await UploadAsync(uploader, SeedIds.OtherClient, "Other Client", "qc04-cross.pdf");
        other.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task Uploader_comments_are_client_visible_and_persist()
    {
        var uploader = await _factory.LoginAsync("uploader@bisconsultants.local");
        var detail = await (await uploader.GetAsync($"/api/work-items/{SeedIds.DemoSurvey}")).ReadJsonAsync();
        detail.GetProperty("canPostComments").GetBoolean().Should().BeTrue();
        detail.GetProperty("canSeeInternalNotes").GetBoolean().Should().BeFalse();
        detail.GetProperty("internalNotes").ValueKind.Should().Be(JsonValueKind.Null);

        var posted = await uploader.PostAsJsonAsync($"/api/work-items/{SeedIds.DemoSurvey}/comments", new
        {
            body = "QC04 client-visible comment from Uploader."
        });
        posted.StatusCode.Should().Be(HttpStatusCode.Created, await posted.Content.ReadAsStringAsync());

        var fresh = await _factory.LoginAsync("uploader@bisconsultants.local");
        var comments = await (await fresh.GetAsync($"/api/work-items/{SeedIds.DemoSurvey}/comments")).ReadJsonAsync();
        comments.EnumerateArray().Select(x => x.GetProperty("body").GetString())
            .Should().Contain("QC04 client-visible comment from Uploader.");

        var viewer = await _factory.LoginAsync("viewer@bisconsultants.local");
        var viewerComments = await (await viewer.GetAsync($"/api/work-items/{SeedIds.DemoSurvey}/comments")).ReadJsonAsync();
        viewerComments.EnumerateArray().Select(x => x.GetProperty("body").GetString())
            .Should().Contain("QC04 client-visible comment from Uploader.");
    }

    [Fact]
    public async Task Uploader_has_no_staff_rights()
    {
        var uploader = await _factory.LoginAsync("uploader@bisconsultants.local");

        (await uploader.PatchAsJsonAsync($"/api/work-items/{SeedIds.DemoPlat}", new { internalNotes = "hidden rewrite" }))
            .StatusCode.Should().Be(HttpStatusCode.Forbidden);
        (await uploader.PatchAsJsonAsync($"/api/work-items/{SeedIds.DemoPlat}", new
        {
            title = "Uploader edit",
            assignedToUserId = SeedIds.EditorDemo,
            statusId = SeedIds.StatusWorked
        })).StatusCode.Should().Be(HttpStatusCode.Forbidden);

        (await uploader.GetAsync("/api/admin/users")).StatusCode.Should().Be(HttpStatusCode.Forbidden);
        (await uploader.PutAsJsonAsync($"/api/admin/organizations/{SeedIds.DemoClient}", new
        {
            name = "Demo Client",
            code = "DEMOCLIENT",
            isActive = true,
            assignedTechIds = new[] { SeedIds.EditorDemo }
        })).StatusCode.Should().Be(HttpStatusCode.Forbidden);

        var notes = await (await uploader.GetAsync($"/api/work-items/{SeedIds.DemoPlat}")).ReadJsonAsync();
        notes.GetProperty("canSeeInternalNotes").GetBoolean().Should().BeFalse();
        notes.GetProperty("canEditInternalNotes").GetBoolean().Should().BeFalse();
        notes.GetProperty("canMutate").GetBoolean().Should().BeFalse();
        notes.GetProperty("internalNotes").ValueKind.Should().Be(JsonValueKind.Null);
    }

    [Fact]
    public async Task Viewer_stays_upload_and_comment_free()
    {
        var viewer = await _factory.LoginAsync("viewer@bisconsultants.local");
        var me = await (await viewer.GetAsync("/api/auth/me")).ReadJsonAsync();
        me.GetProperty("role").GetString().Should().Be("Viewer");
        me.GetProperty("canUpload").GetBoolean().Should().BeFalse();
        me.GetProperty("canPostComments").GetBoolean().Should().BeFalse();

        (await UploadAsync(viewer, SeedIds.DemoClient, "Demo Client", "viewer-blocked.pdf"))
            .StatusCode.Should().Be(HttpStatusCode.Forbidden);
        (await viewer.PostAsJsonAsync($"/api/work-items/{SeedIds.DemoPlat}/comments", new { body = "nope" }))
            .StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    private static async Task<HttpResponseMessage> UploadAsync(
        HttpClient client,
        Guid organizationId,
        string organizationName,
        string fileName)
    {
        using var form = new MultipartFormDataContent();
        form.Add(new StringContent(organizationId.ToString()), "organizationId");
        form.Add(new StringContent(organizationName), "organizationName");
        var file = new ByteArrayContent([0x25, 0x50, 0x44, 0x46, 0x2D]);
        file.Headers.ContentType = new MediaTypeHeaderValue("application/pdf");
        form.Add(file, "file", fileName);
        return await client.PostAsync("/api/work-items", form);
    }
}
