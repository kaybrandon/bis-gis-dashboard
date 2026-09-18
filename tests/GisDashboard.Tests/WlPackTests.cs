using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using FluentAssertions;
using GisDashboard.Application.WorkItems;
using GisDashboard.Domain;
using GisDashboard.Infrastructure.AiFill;
using GisDashboard.Infrastructure.Persistence;

namespace GisDashboard.Tests;

/// <summary>
/// WL01–WL13 verify/close. Linked QC/CR already shipped; this pack locks the Musts
/// and the two WL04 API holes (org auto + ignore client type/priority).
/// WL07 — Status dropdown + Needs Review vocabulary — closed by CR11 (#27 / c9db1fe).
/// Canonical set: Active · Pending · Complete · On-Hold · Cancelled · Needs Review.
/// Fail if: form ≠ charts with no mapping · Needs Review dropped · data destroyed · silent remap.
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

    [Fact]
    public async Task Wl07_canonical_statuses_on_selectors_filters_tiles_charts_exports()
    {
        // WL07 close — CR11 (#27 / c9db1fe) already ships this vocabulary. Evidence only.
        // Fail if: form ≠ charts with no mapping · Needs Review dropped · data destroyed · silent remap.
        var client = await _factory.LoginAsync("admin@bisconsultants.local");
        string[] canonical = ["Active", "Pending", "Complete", "On-Hold", "Cancelled", "Needs Review"];

        var statuses = await (await client.GetAsync("/api/lookups/statuses")).ReadJsonAsync();
        var names = statuses.EnumerateArray().Select(x => x.GetProperty("name").GetString()).ToList();
        names.Should().Equal(canonical,
            "Fail if: selectors/filters do not expose the six canonical statuses.");
        names.Should().Contain("Needs Review", "Fail if: Needs Review dropped.");
        names.Should().NotContain(["Reviewed", "In Progress", "Held", "Worked", "QC'd"],
            "Fail if: form uses unmapped legacy labels or Reviewed as a status.");

        var actions = await (await client.GetAsync("/api/lookups/status-actions")).ReadJsonAsync();
        actions.GetProperty("activeId").GetGuid().Should().Be(SeedIds.StatusInProgress);
        actions.GetProperty("pendingId").GetGuid().Should().Be(SeedIds.StatusPending);
        actions.GetProperty("completeId").GetGuid().Should().Be(SeedIds.StatusWorked);
        actions.GetProperty("onHoldId").GetGuid().Should().Be(SeedIds.StatusHeld);
        actions.GetProperty("cancelledId").GetGuid().Should().Be(SeedIds.StatusCancelled);
        actions.GetProperty("needsReviewId").GetGuid().Should().Be(SeedIds.StatusNeedsReview,
            "Fail if: Needs Review is missing from status selectors.");

        StatusDisplay.CanonicalNames.Should().Equal(canonical);
        StatusDisplay.Label("In Progress").Should().Be("Active",
            "Fail if: form ≠ charts with no mapping (In Progress → Active).");
        StatusDisplay.Label("Held").Should().Be("On-Hold");
        StatusDisplay.Label("Worked").Should().Be("Complete");
        StatusDisplay.Label("Needs Review").Should().Be("Needs Review");
        StatusDisplay.Label("QC'd").Should().Be("QC'd",
            "Fail if: silent remap of QC'd.");

        var qcd = await (await client.GetAsync($"/api/work-items/{SeedIds.DemoDeed}")).ReadJsonAsync();
        qcd.GetProperty("statusId").GetGuid().Should().Be(SeedIds.StatusQcd,
            "Fail if: data destroyed — QC'd row lost its id.");
        qcd.GetProperty("statusName").GetString().Should().Be("QC'd",
            "Fail if: silent remap of stored QC'd data.");
        qcd.GetProperty("isReviewed").GetBoolean().Should().BeFalse();

        var cancelledId = await UploadNamedAsync(client, "wl07-cancelled.pdf");
        (await client.PatchAsJsonAsync($"/api/work-items/{cancelledId}", new
        {
            statusId = SeedIds.StatusCancelled
        })).StatusCode.Should().Be(HttpStatusCode.OK);

        var reviewId = await UploadNamedAsync(client, "wl07-needs-review.pdf");
        var marked = await client.PatchAsJsonAsync($"/api/work-items/{reviewId}", new
        {
            statusId = SeedIds.StatusNeedsReview
        });
        marked.StatusCode.Should().Be(HttpStatusCode.OK);
        var reviewItem = await marked.ReadJsonAsync();
        reviewItem.GetProperty("statusName").GetString().Should().Be("Needs Review");
        reviewItem.GetProperty("isReviewed").GetBoolean().Should().BeFalse(
            "Fail if: Needs Review is treated as Reviewed Yes.");

        var pairs = new (Guid StatusId, string Label)[]
        {
            (SeedIds.StatusInProgress, "Active"),
            (SeedIds.StatusPending, "Pending"),
            (SeedIds.StatusWorked, "Complete"),
            (SeedIds.StatusHeld, "On-Hold"),
            (SeedIds.StatusCancelled, "Cancelled"),
            (SeedIds.StatusNeedsReview, "Needs Review"),
        };
        foreach (var (statusId, label) in pairs)
        {
            var filtered = await (await client.GetAsync(
                $"/api/work-items?pageSize=100&bucket=all&statusId={statusId}")).ReadJsonAsync();
            filtered.GetProperty("items").EnumerateArray().Should().NotBeEmpty(
                "Fail if: {0} filter returns no rows.", label);
            filtered.GetProperty("items").EnumerateArray().Should().OnlyContain(x =>
                x.GetProperty("statusId").GetGuid() == statusId &&
                x.GetProperty("statusName").GetString() == label,
                "Fail if: filter {0} returns a different status label.", label);
        }

        var hold = await (await client.GetAsync("/api/work-items?pageSize=100&bucket=hold")).ReadJsonAsync();
        hold.GetProperty("items").EnumerateArray().Should().OnlyContain(x =>
            x.GetProperty("statusName").GetString() == "On-Hold",
            "Fail if: On-Hold tile/bucket does not use the canonical label.");
        var complete = await (await client.GetAsync("/api/work-items?pageSize=100&bucket=completed")).ReadJsonAsync();
        complete.GetProperty("items").EnumerateArray().Should().OnlyContain(x =>
            x.GetProperty("statusName").GetString() == "Complete" ||
            x.GetProperty("statusId").GetGuid() == SeedIds.StatusQcd,
            "Fail if: Complete tile remaps or drops stored rows.");

        var dash = await (await client.GetAsync("/api/dashboard")).ReadJsonAsync();
        var kpiLabels = dash.GetProperty("kpis").EnumerateArray()
            .Select(x => x.GetProperty("label").GetString()).ToList();
        kpiLabels.Should().Contain(["Active", "Pending"],
            "Fail if: dashboard tiles drop Active/Pending.");
        var chartNames = dash.GetProperty("statusCounts").EnumerateArray()
            .Select(x => x.GetProperty("name").GetString()).ToList();
        chartNames.Should().OnlyContain(name => canonical.Contains(name!),
            "Fail if: form ≠ charts with no mapping — chart stages are outside the canonical set.");
        chartNames.Should().Contain("Needs Review", "Fail if: Needs Review dropped from charts.");
        chartNames.Should().NotContain(["Reviewed", "In Progress", "Held", "Worked", "QC'd"]);

        var csv = await client.GetAsync("/api/work-items/export?pageSize=100&bucket=all");
        csv.StatusCode.Should().Be(HttpStatusCode.OK);
        var csvText = Encoding.UTF8.GetString(await csv.Content.ReadAsByteArrayAsync());
        foreach (var label in canonical)
        {
            csvText.Should().Contain(label, "Fail if: export drops {0}.", label);
        }
        csvText.Should().Contain("QC'd", "Fail if: data destroyed — QC'd export row missing.");
        csvText.Should().NotContain("Reviewed Yes");

        var reviewCsv = await client.GetAsync(
            $"/api/work-items/export?pageSize=100&bucket=all&statusId={SeedIds.StatusNeedsReview}");
        var reviewText = Encoding.UTF8.GetString(await reviewCsv.Content.ReadAsByteArrayAsync());
        reviewText.Should().Contain("wl07-needs-review.pdf");
        reviewText.Should().Contain("Needs Review");
        reviewText.Should().NotContain("Reviewed Yes");

        var pdf = await client.GetAsync("/api/dashboard/pdf");
        pdf.StatusCode.Should().Be(HttpStatusCode.OK);
        var pdfText = PdfTextExtractor.Extract(new MemoryStream(await pdf.Content.ReadAsByteArrayAsync()));
        pdfText.Should().Contain("Active");
        pdfText.Should().Contain("Needs Review", "Fail if: Needs Review dropped from exports.");
        pdfText.Should().NotContain("In Progress");

        var reviewed = await client.PatchAsJsonAsync($"/api/work-items/{reviewId}", new { isReviewed = true });
        reviewed.StatusCode.Should().Be(HttpStatusCode.OK);
        var afterReview = await reviewed.ReadJsonAsync();
        afterReview.GetProperty("statusName").GetString().Should().Be("Needs Review");
        afterReview.GetProperty("isReviewed").GetBoolean().Should().BeTrue(
            "Fail if: Needs Review and Reviewed Yes/No are the same field.");
    }

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
    public async Task Settings_name_the_wl_pack_and_close_wl07()
    {
        var client = await _factory.LoginAsync("admin@bisconsultants.local");
        var note = (await (await client.GetAsync("/api/settings")).ReadJsonAsync())
            .GetProperty("features").GetProperty("wishList").GetProperty("note").GetString();
        note.Should().Contain("WL01");
        note.Should().Contain("WL13");
        note.Should().Contain("WL07");
        note.Should().Contain("CR11");
        note.Should().Contain("Needs Review");
        note.Should().Contain("pack complete");
        note.Should().NotContain("held");
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

    private static async Task<Guid> UploadNamedAsync(HttpClient client, string fileName)
    {
        var response = await UploadAsync(client, SeedIds.DemoClient.ToString(), "Demo Client",
            SeedIds.TypeDeed.ToString(), "Deed", fileName);
        response.StatusCode.Should().Be(HttpStatusCode.OK, await response.Content.ReadAsStringAsync());
        return (await response.ReadJsonAsync()).GetProperty("id").GetGuid();
    }
}
