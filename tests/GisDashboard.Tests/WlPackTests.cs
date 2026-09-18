using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using FluentAssertions;
using GisDashboard.Application.WorkItems;
using GisDashboard.Domain;
using GisDashboard.Infrastructure.Persistence;

namespace GisDashboard.Tests;

/// <summary>
/// WL01–WL13 verify/close. Linked QC/CR already shipped; this pack locks the Musts
/// and the two WL04 API holes (org auto + ignore client type/priority).
/// WL07 — Status dropdown + Needs Review vocabulary — HOLD until CR11 (#27) is on main.
/// Do not change status names, selectors, tiles, or charts here.
/// </summary>
public sealed class WlPackTests : IClassFixture<ApiFactory>
{
    private readonly ApiFactory _factory;

    public WlPackTests(ApiFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task Wl01_staff_roles_see_every_organization()
    {
        foreach (var email in new[]
        {
            "admin@bisconsultants.local",
            "admin@democlient.local",
            "editor@bisconsultants.local"
        })
        {
            var client = await _factory.LoginAsync(email);
            var me = await (await client.GetAsync("/api/auth/me")).ReadJsonAsync();
            me.GetProperty("canSeeAllOrganizations").GetBoolean().Should().BeTrue(email);
            var orgs = await (await client.GetAsync("/api/lookups/organizations")).ReadJsonAsync();
            orgs.EnumerateArray().Select(x => x.GetProperty("name").GetString())
                .Should().Contain(new[] { "Demo Client", "Other Client" },
                    "Fail if: Global Admin / Admin / Editor cannot see every organization.");
            (await client.GetAsync($"/api/work-items/{SeedIds.DemoPlat}")).StatusCode.Should().Be(HttpStatusCode.OK);
            (await client.GetAsync($"/api/work-items/{SeedIds.OtherPlat}")).StatusCode.Should().Be(HttpStatusCode.OK);
        }

        Roles.CanSeeAllOrganizations(Roles.GlobalAdministrator).Should().BeTrue();
        Roles.CanSeeAllOrganizations(Roles.Administrator).Should().BeTrue();
        Roles.CanSeeAllOrganizations(Roles.Editor).Should().BeTrue();
        Roles.CanSeeAllOrganizations(Roles.Uploader).Should().BeFalse();
        Roles.CanSeeAllOrganizations(Roles.Viewer).Should().BeFalse();
    }

    [Fact]
    public async Task Wl02_uploader_uploads_only_into_assigned_org()
    {
        var uploader = await _factory.LoginAsync("uploader@bisconsultants.local");
        var own = await UploadAsync(uploader, SeedIds.DemoClient.ToString(), "Demo Client", "", "", "wl02-own.pdf");
        own.StatusCode.Should().Be(HttpStatusCode.OK, await own.Content.ReadAsStringAsync());
        (await own.ReadJsonAsync()).GetProperty("organizationName").GetString().Should().Be("Demo Client",
            "Fail if: Uploader cannot put a file into the assigned organization.");

        var other = await UploadAsync(uploader, SeedIds.OtherClient.ToString(), "Other Client", "", "", "wl02-cross.pdf");
        other.StatusCode.Should().Be(HttpStatusCode.NotFound,
            "Fail if: Uploader can upload into an organization they are not assigned to.");
    }

    [Fact]
    public async Task Wl03_mass_upload_is_separate_from_manage_documents_and_is_multi_file()
    {
        var admin = await _factory.LoginAsync("admin@bisconsultants.local");
        var features = (await (await admin.GetAsync("/api/settings")).ReadJsonAsync()).GetProperty("features");
        features.GetProperty("uploadDocuments").GetProperty("enabled").GetBoolean().Should().BeTrue(
            "Fail if: Upload Documents is not its own page.");
        features.GetProperty("massUpload").GetProperty("enabled").GetBoolean().Should().BeTrue();
        features.GetProperty("uploadDocuments").GetProperty("note").GetString()
            .Should().Contain("/upload-documents");
        features.GetProperty("massUpload").GetProperty("note").GetString()
            .Should().Contain("Each file becomes its own work item");

        var first = await UploadAsync(admin, SeedIds.DemoClient.ToString(), "Demo Client", SeedIds.TypeDeed.ToString(), "Deed", "wl03-a.pdf");
        var second = await UploadAsync(admin, SeedIds.DemoClient.ToString(), "Demo Client", SeedIds.TypeDeed.ToString(), "Deed", "wl03-b.pdf");
        first.StatusCode.Should().Be(HttpStatusCode.OK);
        second.StatusCode.Should().Be(HttpStatusCode.OK);
        (await first.ReadJsonAsync()).GetProperty("id").GetGuid()
            .Should().NotBe((await second.ReadJsonAsync()).GetProperty("id").GetGuid(),
                "Fail if: two files in one batch collapse into one work item.");
    }

    [Fact]
    public async Task Wl04_uploader_org_is_automatic_and_type_priority_are_staff_only()
    {
        var uploader = await _factory.LoginAsync("uploader@bisconsultants.local");
        var autoOrg = await UploadAsync(uploader, "", "", SeedIds.TypeDeed.ToString(), "Deed", "wl04-auto-org.pdf",
            isPriority: true, priorityNote: "Needed Friday");
        autoOrg.StatusCode.Should().Be(HttpStatusCode.OK, await autoOrg.Content.ReadAsStringAsync());
        var created = await autoOrg.ReadJsonAsync();
        created.GetProperty("organizationName").GetString().Should().Be("Demo Client",
            "Fail if: a single-org Uploader must pick an organization.");
        created.GetProperty("documentTypeName").GetString().Should().Be("Other",
            "Fail if: Uploader-supplied document type is honored. Staff set type later.");
        created.GetProperty("isPriority").GetBoolean().Should().BeFalse(
            "Fail if: Uploader-supplied priority is honored. Staff set priority later.");

        var editor = await _factory.LoginAsync("editor@bisconsultants.local");
        var staff = await UploadAsync(editor, SeedIds.DemoClient.ToString(), "Demo Client",
            SeedIds.TypePlat.ToString(), "Plat", "wl04-staff.pdf", isPriority: true, priorityNote: "Staff priority");
        staff.StatusCode.Should().Be(HttpStatusCode.OK, await staff.Content.ReadAsStringAsync());
        var staffItem = await staff.ReadJsonAsync();
        staffItem.GetProperty("documentTypeName").GetString().Should().Be("Plat",
            "Fail if: staff cannot set document type on Upload.");
        staffItem.GetProperty("isPriority").GetBoolean().Should().BeTrue(
            "Fail if: staff cannot set priority on Upload.");
        staffItem.GetProperty("priorityNote").GetString().Should().Be("Staff priority");
    }

    [Fact]
    public async Task Wl04_multi_org_uploader_still_must_choose_a_client()
    {
        var admin = await _factory.LoginAsync("admin@bisconsultants.local");
        var created = await admin.PostAsJsonAsync("/api/admin/users", new
        {
            email = "wl04.multi@democlient.local",
            password = "Demo!Gis2026",
            displayName = "WL04 Multi",
            role = "Uploader",
            organizationIds = new[] { SeedIds.DemoClient, SeedIds.OtherClient }
        });
        created.StatusCode.Should().Be(HttpStatusCode.OK, await created.Content.ReadAsStringAsync());

        var uploader = await _factory.LoginAsync("wl04.multi@democlient.local");
        var missing = await UploadAsync(uploader, "", "", "", "", "wl04-multi-missing.pdf");
        missing.StatusCode.Should().Be(HttpStatusCode.BadRequest,
            "Fail if: a multi-org Uploader can upload without choosing a client.");
    }

    [Fact]
    public async Task Wl05_staff_pick_org_and_batch_stays_on_that_org()
    {
        var editor = await _factory.LoginAsync("editor@bisconsultants.local");
        var first = await UploadAsync(editor, SeedIds.OtherClient.ToString(), "Other Client",
            SeedIds.TypeSurvey.ToString(), "Survey", "wl05-a.pdf", isPriority: true, priorityNote: "Batch note");
        var second = await UploadAsync(editor, SeedIds.OtherClient.ToString(), "Other Client",
            SeedIds.TypeSurvey.ToString(), "Survey", "wl05-b.pdf", isPriority: true, priorityNote: "Batch note");
        first.StatusCode.Should().Be(HttpStatusCode.OK, await first.Content.ReadAsStringAsync());
        second.StatusCode.Should().Be(HttpStatusCode.OK, await second.Content.ReadAsStringAsync());
        var a = await first.ReadJsonAsync();
        var b = await second.ReadJsonAsync();
        a.GetProperty("organizationName").GetString().Should().Be("Other Client",
            "Fail if: staff cannot select the client on Upload.");
        b.GetProperty("organizationName").GetString().Should().Be("Other Client",
            "Fail if: files in one staff batch land in different organizations.");
        a.GetProperty("documentTypeName").GetString().Should().Be("Survey");
        b.GetProperty("documentTypeName").GetString().Should().Be("Survey");
        a.GetProperty("isPriority").GetBoolean().Should().BeTrue();
        b.GetProperty("isPriority").GetBoolean().Should().BeTrue();

        var missing = await UploadAsync(editor, "", "", SeedIds.TypeDeed.ToString(), "Deed", "wl05-no-org.pdf");
        missing.StatusCode.Should().Be(HttpStatusCode.BadRequest,
            "Fail if: staff upload invents an organization when none is selected.");
    }

    [Fact]
    public async Task Wl06_upload_page_is_left_nav_and_manage_documents_plus_upload()
    {
        var admin = await _factory.LoginAsync("admin@bisconsultants.local");
        var note = (await (await admin.GetAsync("/api/settings")).ReadJsonAsync())
            .GetProperty("features").GetProperty("uploadDocuments").GetProperty("note").GetString();
        note.Should().Contain("Left-nav", "Fail if: Upload Documents is not a left-menu page.");
        note.Should().Contain("+ Upload", "Fail if: Manage Documents + Upload does not go to the Upload page.");
        note.Should().Contain("/upload-documents");
    }

    // WL07 HOLD — Status dropdown + Needs Review vocabulary depends on CR11 (#27).
    // Do not change status names, selectors, tiles, charts, or exports here until CR11 is on main.

    [Fact]
    public async Task Wl08_document_types_are_deed_plat_survey_subdivision_other()
    {
        var client = await _factory.LoginAsync("admin@bisconsultants.local");
        var types = await (await client.GetAsync("/api/lookups/document-types")).ReadJsonAsync();
        types.EnumerateArray().Select(x => x.GetProperty("name").GetString()).Should().Equal(
            ["Deed", "Plat", "Survey", "Subdivision", "Other"],
            "Fail if: document type order is not Deed, Plat, Survey, Subdivision, Other.");
    }

    [Fact]
    public async Task Wl09_new_upload_auto_assigns_org_assigned_technician()
    {
        var admin = await _factory.LoginAsync("admin@bisconsultants.local");
        var response = await UploadAsync(admin, SeedIds.DemoClient.ToString(), "Demo Client",
            SeedIds.TypeDeed.ToString(), "Deed", "wl09-auto.pdf");
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var json = await response.ReadJsonAsync();
        json.GetProperty("assignedToUserId").GetGuid().Should().Be(SeedIds.EditorDemo,
            "Fail if: Assigned to is not auto-set from the org Assigned technician.");
        json.GetProperty("assignedToName").GetString().Should().Be("Alex Rivera");
    }

    [Fact]
    public async Task Wl10_and_wl11_reviewed_is_yes_no_and_independent_of_hours()
    {
        var editor = await _factory.LoginAsync("editor@bisconsultants.local");
        var uploaded = await UploadAsync(editor, SeedIds.DemoClient.ToString(), "Demo Client",
            SeedIds.TypeDeed.ToString(), "Deed", "wl10-reviewed.pdf");
        var id = (await uploaded.ReadJsonAsync()).GetProperty("id").GetGuid();

        var marked = await editor.PatchAsJsonAsync($"/api/work-items/{id}", new { isReviewed = true });
        marked.StatusCode.Should().Be(HttpStatusCode.OK);
        (await marked.ReadJsonAsync()).GetProperty("isReviewed").GetBoolean().Should().BeTrue(
            "Fail if: Reviewed cannot be set next to Priority on the work item.");

        var list = await (await editor.GetAsync("/api/work-items?pageSize=100")).ReadJsonAsync();
        var row = list.GetProperty("items").EnumerateArray()
            .Single(x => x.GetProperty("id").GetGuid() == id);
        row.GetProperty("isReviewed").GetBoolean().Should().BeTrue(
            "Fail if: Manage Documents does not expose Reviewed Yes/No.");
        row.TryGetProperty("hoursLabel", out _).Should().BeTrue();

        var settings = await (await editor.GetAsync("/api/settings")).ReadJsonAsync();
        var reviewed = settings.GetProperty("features").GetProperty("reviewed").GetProperty("note").GetString();
        reviewed.Should().Contain("next to Priority");
        reviewed.Should().Contain("Review yes/no");
    }

    [Fact]
    public void Wl12_tiff_and_browser_images_are_previewable()
    {
        DocumentPreview.IsTiff("scan.tif", "image/tiff").Should().BeTrue(
            "Fail if: TIFF is not treated as a first-page preview.");
        DocumentPreview.IsBrowserImage("photo.jpg", "image/jpeg").Should().BeTrue(
            "Fail if: JPEG/PNG image preview is dropped.");
        DocumentPreview.IsBrowserImage("scan.tif", "image/tiff").Should().BeFalse();
        UploadFileTypes.TryResolve("scan.tif", "image/tiff", out var tiff).Should().BeTrue();
        tiff.Should().Be("image/tiff");
        UploadFileTypes.TryResolve("photo.jpg", "image/jpeg", out var jpeg).Should().BeTrue();
        jpeg.Should().Be("image/jpeg");
    }

    [Fact]
    public async Task Wl13_staff_keep_signed_in_assignee_scope_and_clients_do_not()
    {
        var editor = await _factory.LoginAsync("editor@bisconsultants.local");
        var me = await (await editor.GetAsync("/api/auth/me")).ReadJsonAsync();
        me.GetProperty("canSeeDashboardAssignee").GetBoolean().Should().BeTrue();
        me.GetProperty("id").GetGuid().Should().Be(SeedIds.EditorDemo);

        var mine = await (await editor.GetAsync(
            $"/api/work-items?assignedToUserId={SeedIds.EditorDemo}&pageSize=100")).ReadJsonAsync();
        mine.GetProperty("items").EnumerateArray()
            .Should().OnlyContain(x => x.GetProperty("assignedToUserId").GetGuid() == SeedIds.EditorDemo,
                "Fail if: staff Manage Documents cannot default to the signed-in Assigned to.");

        var viewer = await _factory.LoginAsync("viewer@bisconsultants.local");
        var viewerMe = await (await viewer.GetAsync("/api/auth/me")).ReadJsonAsync();
        viewerMe.GetProperty("canSeeDashboardAssignee").GetBoolean().Should().BeFalse(
            "Fail if: Viewer/Uploader get an Assignee control (QC08).");
    }

    [Fact]
    public async Task Settings_name_the_wl_pack_and_hold_wl07()
    {
        var client = await _factory.LoginAsync("admin@bisconsultants.local");
        var note = (await (await client.GetAsync("/api/settings")).ReadJsonAsync())
            .GetProperty("features").GetProperty("wishList").GetProperty("note").GetString();
        note.Should().Contain("WL01");
        note.Should().Contain("WL13");
        note.Should().Contain("WL07");
        note.Should().Contain("CR11");
        note.Should().Contain("held");
    }

    private static async Task<HttpResponseMessage> UploadAsync(
        HttpClient client,
        string organizationId,
        string organizationName,
        string documentTypeId,
        string documentTypeName,
        string fileName,
        bool isPriority = false,
        string? priorityNote = null)
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

        if (isPriority)
        {
            form.Add(new StringContent("true"), "isPriority");
        }

        if (!string.IsNullOrEmpty(priorityNote))
        {
            form.Add(new StringContent(priorityNote), "priorityNote");
        }

        var file = new ByteArrayContent([0x25, 0x50, 0x44, 0x46, 0x2D]);
        file.Headers.ContentType = new MediaTypeHeaderValue("application/pdf");
        form.Add(file, "file", fileName);
        return await client.PostAsync("/api/work-items", form);
    }
}
