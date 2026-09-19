using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using FluentAssertions;
using GisDashboard.Application.AiFill;
using GisDashboard.Infrastructure.Persistence;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.DependencyInjection;

namespace GisDashboard.Tests;

public sealed class AiFillTests : IClassFixture<ApiFactory>
{
    private readonly ApiFactory _factory;

    public AiFillTests(ApiFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task Unconfigured_ai_fill_fails_closed()
    {
        var client = await _factory.LoginAsync("editor@bisconsultants.local");
        var response = await client.PostAsync($"/api/work-items/{SeedIds.DemoPlat}/ai-fill", null);
        response.StatusCode.Should().Be(HttpStatusCode.ServiceUnavailable);
        var json = await response.ReadJsonAsync();
        json.GetProperty("message").GetString().Should().Contain("AzureOpenAI__Endpoint");
        json.GetProperty("message").GetString().Should().Contain("AzureOpenAI__ApiKey");

        var before = await (await client.GetAsync($"/api/work-items/{SeedIds.DemoPlat}")).ReadJsonAsync();
        before.GetProperty("title").GetString().Should().Be("Northridge Addition, Block 4");
    }

    [Fact]
    public async Task Viewer_cannot_ai_fill()
    {
        var viewer = await _factory.LoginAsync("viewer@bisconsultants.local");
        (await viewer.PostAsync($"/api/work-items/{SeedIds.DemoPlat}/ai-fill", null))
            .StatusCode.Should().Be(HttpStatusCode.Forbidden);
        (await viewer.PostAsync($"/api/work-items/{SeedIds.OtherPlat}/ai-fill", null))
            .StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task Anonymous_ai_fill_is_unauthorized()
    {
        var anon = _factory.CreateClient();
        (await anon.PostAsync($"/api/work-items/{SeedIds.DemoPlat}/ai-fill", null))
            .StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Status_lists_azure_openai_as_configured_only()
    {
        var client = await _factory.LoginAsync("admin@bisconsultants.local");
        var json = await (await client.GetAsync("/api/admin/status")).ReadJsonAsync();
        var checks = json.GetProperty("checks").EnumerateArray().ToDictionary(x => x.GetProperty("key").GetString()!);
        checks.Should().ContainKey("azureOpenAI");
        checks["azureOpenAI"].GetProperty("status").GetString().Should().Be("not_configured");
        checks["azureOpenAI"].GetProperty("mode").GetString().Should().Be("configured");
        checks["azureOpenAI"].GetProperty("detail").GetString().Should().Contain("gpt-4.1-mini");
    }

    [Fact]
    public async Task Settings_mentions_ai_fill()
    {
        var client = await _factory.LoginAsync("admin@bisconsultants.local");
        var json = await (await client.GetAsync("/api/settings")).ReadJsonAsync();
        json.GetProperty("features").GetProperty("aiFillFromPdf").GetProperty("enabled").GetBoolean().Should().BeTrue();
        json.GetProperty("features").GetProperty("aiFillFromPdf").GetProperty("note").GetString().Should().Contain("Easy / Medium / Hard");
        json.GetProperty("features").GetProperty("aiFillFromPdf").GetProperty("note").GetString().Should().Contain("gpt-4.1-mini");
        json.GetProperty("features").GetProperty("aiFillFromPdf").GetProperty("note").GetString().Should().Contain("scanned");
        json.GetProperty("features").GetProperty("aiFillFromPdf").GetProperty("note").GetString().Should().Contain("automatically");
        json.GetProperty("features").GetProperty("aiFillFromPdf").GetProperty("note").GetString().Should().Contain("manual-only");
        json.GetProperty("features").GetProperty("aiFillFromPdf").GetProperty("note").GetString().Should().Contain("Property IDs");
    }

    [Fact]
    public async Task Unconfigured_upload_succeeds_with_ai_scan_failed_state()
    {
        var client = await _factory.LoginAsync("editor@bisconsultants.local");
        var uploaded = await AiFillUpload.PostAsync(client, AiFillUpload.MinimalTextPdf(), "fresh-upload.pdf", "Fresh upload");
        uploaded.StatusCode.Should().Be(HttpStatusCode.OK, await uploaded.Content.ReadAsStringAsync());
        var json = await uploaded.ReadJsonAsync();
        json.GetProperty("title").GetString().Should().Be("Fresh upload");
        json.GetProperty("aiScan").GetProperty("status").GetString().Should().Be("unconfigured");
        json.GetProperty("aiScan").GetProperty("message").GetString().Should().Contain("AI scan failed");
        json.GetProperty("aiScan").GetProperty("message").GetString().Should().Contain("not configured");
        json.GetRawText().Should().NotContain("no extractable text");
        json.GetRawText().Should().NotContain("cannot be AI-filled");
    }
}

public sealed class AiFillConfiguredTests : IClassFixture<AiFillConfiguredFactory>
{
    private readonly AiFillConfiguredFactory _factory;

    public AiFillConfiguredTests(AiFillConfiguredFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task Configured_fill_returns_fields_and_does_not_persist()
    {
        ScriptedOpenAiCompletions.Reset();
        var client = await _factory.LoginAsync("editor@bisconsultants.local");
        var before = await (await client.GetAsync($"/api/work-items/{SeedIds.DemoPlat}")).ReadJsonAsync();
        var originalTitle = before.GetProperty("title").GetString();
        var originalStatus = before.GetProperty("statusId").GetGuid();
        var originalAssignee = before.GetProperty("assignedToUserId").GetGuid();

        var response = await client.PostAsync($"/api/work-items/{SeedIds.DemoPlat}/ai-fill", null);
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var json = await response.ReadJsonAsync();
        json.GetProperty("overallConfidence").GetDouble().Should().BeApproximately(0.84, 0.001);
        json.GetProperty("deployment").GetString().Should().Be("gpt-4.1-mini");
        json.GetProperty("fields").GetProperty("title").GetProperty("present").GetBoolean().Should().BeTrue();
        json.GetProperty("fields").GetProperty("title").GetProperty("value").GetString().Should().Be("N-14-042 Final Plat");
        json.GetProperty("fields").GetProperty("type").GetProperty("documentTypeId").GetGuid().Should().Be(SeedIds.TypePlat);
        json.GetProperty("fields").GetProperty("propertyIds").GetProperty("present").GetBoolean().Should().BeFalse();
        AssertEmptyPropertyIds(json.GetProperty("fields").GetProperty("propertyIds"));
        json.GetProperty("fields").GetProperty("platCount").GetProperty("value").GetInt32().Should().Be(1);
        json.GetProperty("fields").GetProperty("workedOn").GetProperty("value").GetString().Should().Be("2026-03-15");
        json.GetProperty("difficulty").GetProperty("band").GetString().Should().Be("Easy");
        json.GetProperty("difficulty").GetProperty("why").GetString().Should().NotBeNullOrWhiteSpace();
        json.GetProperty("difficulty").GetProperty("reasons").GetArrayLength().Should().BeGreaterThan(0);
        json.GetProperty("difficulty").GetProperty("overridden").GetBoolean().Should().BeFalse();

        var after = await (await client.GetAsync($"/api/work-items/{SeedIds.DemoPlat}")).ReadJsonAsync();
        after.GetProperty("title").GetString().Should().Be(originalTitle);
        after.GetProperty("statusId").GetGuid().Should().Be(originalStatus);
        after.GetProperty("assignedToUserId").GetGuid().Should().Be(originalAssignee);
        after.GetProperty("isSplit").GetBoolean().Should().Be(before.GetProperty("isSplit").GetBoolean());
        after.GetProperty("isSketch").GetBoolean().Should().Be(before.GetProperty("isSketch").GetBoolean());
        after.GetProperty("isPriority").GetBoolean().Should().Be(before.GetProperty("isPriority").GetBoolean());
        after.GetProperty("isReviewed").GetBoolean().Should().Be(before.GetProperty("isReviewed").GetBoolean());
        after.GetProperty("propertyIds").GetString().Should().Be(before.GetProperty("propertyIds").GetString());
        after.GetProperty("propertyIds").GetString().Should().Contain("R12345");
        after.GetProperty("difficulty").GetProperty("band").GetString().Should().Be("Easy");
        after.GetProperty("difficulty").GetProperty("why").GetString().Should().Contain("Lot-and-block");
        ScriptedOpenAiCompletions.VisionCalls.Should().Be(0);
        ScriptedOpenAiCompletions.LastImageCount.Should().Be(0);
        var listed = await (await client.GetAsync("/api/work-items?pageSize=100")).ReadJsonAsync();
        listed.GetProperty("items").EnumerateArray()
            .Should().Contain(item =>
                item.GetProperty("id").GetGuid() == SeedIds.DemoPlat
                && item.GetProperty("difficulty").GetProperty("band").GetString() == "Easy");
    }

    [Fact]
    public async Task Staff_override_sticks_until_confirmed_rescore()
    {
        ScriptedOpenAiCompletions.Reset();
        var client = await _factory.LoginAsync("editor@bisconsultants.local");
        (await client.PostAsync($"/api/work-items/{SeedIds.DemoPlat}/ai-fill", null)).StatusCode.Should().Be(HttpStatusCode.OK);

        var overrideResponse = await client.PatchAsJsonAsync($"/api/work-items/{SeedIds.DemoPlat}", new { difficultyBand = "Hard" });
        overrideResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        var overridden = await overrideResponse.ReadJsonAsync();
        overridden.GetProperty("difficulty").GetProperty("band").GetString().Should().Be("Hard");
        overridden.GetProperty("difficulty").GetProperty("overridden").GetBoolean().Should().BeTrue();

        var cleared = await client.PatchAsJsonAsync($"/api/work-items/{SeedIds.DemoPlat}", new { clearDifficultyOverride = true });
        cleared.StatusCode.Should().Be(HttpStatusCode.OK);
        var restored = await cleared.ReadJsonAsync();
        restored.GetProperty("difficulty").GetProperty("band").GetString().Should().Be("Easy");
        restored.GetProperty("difficulty").GetProperty("overridden").GetBoolean().Should().BeFalse();

        (await client.PatchAsJsonAsync($"/api/work-items/{SeedIds.DemoPlat}", new { difficultyBand = "Hard" }))
            .StatusCode.Should().Be(HttpStatusCode.OK);

        ScriptedOpenAiCompletions.ResponseJson = ScriptedOpenAiCompletions.JsonWithDifficulty("Medium", "Several parcels and a longer legal.");
        var refill = await (await client.PostAsync($"/api/work-items/{SeedIds.DemoPlat}/ai-fill", null)).ReadJsonAsync();
        refill.GetProperty("difficulty").GetProperty("band").GetString().Should().Be("Hard");
        refill.GetProperty("difficulty").GetProperty("overridden").GetBoolean().Should().BeTrue();
        refill.GetProperty("difficulty").GetProperty("keptOverride").GetBoolean().Should().BeTrue();
        refill.GetProperty("difficulty").GetProperty("aiBand").GetString().Should().Be("Medium");

        var stillHard = await (await client.GetAsync($"/api/work-items/{SeedIds.DemoPlat}")).ReadJsonAsync();
        stillHard.GetProperty("difficulty").GetProperty("band").GetString().Should().Be("Hard");
        stillHard.GetProperty("difficulty").GetProperty("overridden").GetBoolean().Should().BeTrue();

        var rescored = await (await client.PostAsync($"/api/work-items/{SeedIds.DemoPlat}/ai-fill?rescore=true", null)).ReadJsonAsync();
        rescored.GetProperty("difficulty").GetProperty("band").GetString().Should().Be("Medium");
        rescored.GetProperty("difficulty").GetProperty("overridden").GetBoolean().Should().BeFalse();
        rescored.GetProperty("difficulty").GetProperty("keptOverride").GetBoolean().Should().BeFalse();

        var afterRescore = await (await client.GetAsync($"/api/work-items/{SeedIds.DemoPlat}")).ReadJsonAsync();
        afterRescore.GetProperty("difficulty").GetProperty("band").GetString().Should().Be("Medium");
        afterRescore.GetProperty("difficulty").GetProperty("overridden").GetBoolean().Should().BeFalse();
    }

    [Fact]
    public async Task Viewer_cannot_override_difficulty()
    {
        ScriptedOpenAiCompletions.Reset();
        var editor = await _factory.LoginAsync("editor@bisconsultants.local");
        (await editor.PostAsync($"/api/work-items/{SeedIds.DemoPlat}/ai-fill", null)).StatusCode.Should().Be(HttpStatusCode.OK);

        var viewer = await _factory.LoginAsync("viewer@bisconsultants.local");
        var response = await viewer.PatchAsJsonAsync($"/api/work-items/{SeedIds.DemoPlat}", new { difficultyBand = "Hard" });
        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
        var detail = await (await viewer.GetAsync($"/api/work-items/{SeedIds.DemoPlat}")).ReadJsonAsync();
        detail.GetProperty("difficulty").GetProperty("band").GetString().Should().Be("Easy");
        detail.GetProperty("difficulty").GetProperty("overridden").GetBoolean().Should().BeFalse();
    }

    [Fact]
    public async Task Missing_model_difficulty_still_persists_score_and_why()
    {
        ScriptedOpenAiCompletions.Reset();
        ScriptedOpenAiCompletions.ResponseJson = ScriptedOpenAiCompletions.JsonWithoutDifficulty();
        var client = await _factory.LoginAsync("editor@bisconsultants.local");
        var json = await (await client.PostAsync($"/api/work-items/{SeedIds.DemoOakGrove}/ai-fill", null)).ReadJsonAsync();
        json.GetProperty("difficulty").GetProperty("band").GetString().Should().BeOneOf("Easy", "Medium", "Hard");
        json.GetProperty("difficulty").GetProperty("why").GetString().Should().NotBeNullOrWhiteSpace();
        json.GetProperty("difficulty").GetProperty("reasons").GetArrayLength().Should().BeGreaterThan(0);

        var after = await (await client.GetAsync($"/api/work-items/{SeedIds.DemoOakGrove}")).ReadJsonAsync();
        after.GetProperty("difficulty").GetProperty("band").GetString().Should().Be(json.GetProperty("difficulty").GetProperty("band").GetString());
        after.GetProperty("title").GetString().Should().NotBe("N-14-042 Final Plat");
    }

    [Fact]
    public async Task Image_work_item_is_rejected()
    {
        var client = await _factory.LoginAsync("editor@bisconsultants.local");
        var response = await client.PostAsync($"/api/work-items/{SeedIds.DemoDeed}/ai-fill", null);
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await response.ReadJsonAsync()).GetProperty("message").GetString().Should().Contain("PDF");
    }

    [Fact]
    public async Task Empty_text_pdf_auto_scans_via_vision_and_persists_difficulty()
    {
        ScriptedOpenAiCompletions.Reset();
        var client = await _factory.LoginAsync("admin@bisconsultants.local");
        var uploaded = await AiFillUpload.PostAsync(client, EmptyPagePdf(), "empty-scan.pdf", "Empty scan");
        uploaded.StatusCode.Should().Be(HttpStatusCode.OK);
        var id = (await uploaded.ReadJsonAsync()).GetProperty("id").GetGuid();

        var after = await AiFillUpload.WaitForScanAsync(client, id);
        after.GetRawText().Should().NotContain("no extractable text");
        after.GetRawText().Should().NotContain("cannot be AI-filled");
        after.GetProperty("aiScan").GetProperty("status").GetString().Should().Be("succeeded");
        after.GetProperty("aiScan").GetProperty("result").GetProperty("fields").GetProperty("title").GetProperty("value").GetString()
            .Should().Be("N-14-042 Final Plat");
        after.GetProperty("aiScan").GetProperty("result").GetProperty("difficulty").GetProperty("band").GetString().Should().Be("Easy");
        after.GetProperty("difficulty").GetProperty("band").GetString().Should().Be("Easy");
        after.GetProperty("difficulty").GetProperty("why").GetString().Should().NotBeNullOrWhiteSpace();
        after.GetProperty("title").GetString().Should().Be("Empty scan");
        ScriptedOpenAiCompletions.Calls.Should().Be(1);
        ScriptedOpenAiCompletions.VisionCalls.Should().Be(1);
        ScriptedOpenAiCompletions.LastImageCount.Should().BeGreaterThan(0);
        ScriptedOpenAiCompletions.LastSystemPrompt.Should().Contain("page images");
        ScriptedOpenAiCompletions.LastSystemPrompt.Should().Contain("difficulty");
        ScriptedOpenAiCompletions.LastSystemPrompt.Should().Contain("manual");
        AssertEmptyPropertyIds(after.GetProperty("aiScan").GetProperty("result").GetProperty("fields").GetProperty("propertyIds"));
    }

    [Fact]
    public async Task Image_only_pdf_auto_scans_via_vision_and_scores_difficulty_on_same_pass()
    {
        ScriptedOpenAiCompletions.Reset();
        ScriptedOpenAiCompletions.ResponseJson = ScriptedOpenAiCompletions.JsonWithDifficulty("Hard", "Scanned metes-and-bounds deed.");
        var client = await _factory.LoginAsync("admin@bisconsultants.local");
        var uploaded = await AiFillUpload.PostAsync(client, ImageOnlyPdf(), "scanned-deed.pdf", "Scanned deed");
        uploaded.StatusCode.Should().Be(HttpStatusCode.OK);
        var id = (await uploaded.ReadJsonAsync()).GetProperty("id").GetGuid();

        var after = await AiFillUpload.WaitForScanAsync(client, id);
        after.GetProperty("aiScan").GetProperty("status").GetString().Should().Be("succeeded");
        after.GetProperty("aiScan").GetProperty("result").GetProperty("fields").GetProperty("type").GetProperty("documentTypeId").GetGuid()
            .Should().Be(SeedIds.TypePlat);
        after.GetProperty("difficulty").GetProperty("band").GetString().Should().Be("Hard");
        after.GetProperty("difficulty").GetProperty("why").GetString().Should().Contain("Scanned");
        after.GetProperty("title").GetString().Should().Be("Scanned deed");
        after.GetRawText().Should().NotContain("no extractable text");
        ScriptedOpenAiCompletions.VisionCalls.Should().Be(1);
        ScriptedOpenAiCompletions.LastImageCount.Should().BeGreaterThan(0);
    }

    [Fact]
    public async Task Cad_web_map_property_ids_are_never_filled()
    {
        ScriptedOpenAiCompletions.Reset();
        ScriptedOpenAiCompletions.ResponseJson = ScriptedOpenAiCompletions.CadMapDumpJson();
        var client = await _factory.LoginAsync("editor@bisconsultants.local");
        var before = await (await client.GetAsync($"/api/work-items/{SeedIds.DemoPlat}")).ReadJsonAsync();
        var json = await (await client.PostAsync($"/api/work-items/{SeedIds.DemoPlat}/ai-fill", null)).ReadJsonAsync();
        json.GetProperty("fields").GetProperty("propertyIds").GetProperty("present").GetBoolean().Should().BeFalse();
        AssertEmptyPropertyIds(json.GetProperty("fields").GetProperty("propertyIds"));

        json.GetRawText().Should().NotContain("[\"11402\"");
        json.GetProperty("fields").GetProperty("title").GetProperty("present").GetBoolean().Should().BeTrue();
        ScriptedOpenAiCompletions.LastSystemPrompt.Should().Contain("manual");
        ScriptedOpenAiCompletions.LastSystemPrompt.Should().Contain("Do not extract or return propertyIds");

        var after = await (await client.GetAsync($"/api/work-items/{SeedIds.DemoPlat}")).ReadJsonAsync();
        after.GetProperty("propertyIds").GetString().Should().Be(before.GetProperty("propertyIds").GetString());
    }

    [Fact]
    public async Task Model_property_ids_are_dropped_and_saved_values_persist()
    {
        ScriptedOpenAiCompletions.Reset();
        ScriptedOpenAiCompletions.ResponseJson = ScriptedOpenAiCompletions.JsonWithPropertyIds("""["R701", "R702"]""");
        var client = await _factory.LoginAsync("editor@bisconsultants.local");
        var patched = await client.PatchAsJsonAsync($"/api/work-items/{SeedIds.DemoOakGrove}", new { propertyIds = "MANUAL-1\nMANUAL-2" });
        patched.StatusCode.Should().Be(HttpStatusCode.OK);

        var json = await (await client.PostAsync($"/api/work-items/{SeedIds.DemoOakGrove}/ai-fill", null)).ReadJsonAsync();
        json.GetProperty("fields").GetProperty("propertyIds").GetProperty("present").GetBoolean().Should().BeFalse();
        AssertEmptyPropertyIds(json.GetProperty("fields").GetProperty("propertyIds"));
        json.GetProperty("fields").GetProperty("title").GetProperty("present").GetBoolean().Should().BeTrue();

        var after = await (await client.GetAsync($"/api/work-items/{SeedIds.DemoOakGrove}")).ReadJsonAsync();
        after.GetProperty("propertyIds").GetString().Should().Be("MANUAL-1\nMANUAL-2");
    }

    [Fact]
    public async Task Duplicate_in_flight_scan_does_not_double_charge()
    {
        ScriptedOpenAiCompletions.Reset();
        ScriptedOpenAiCompletions.DelayMs = 400;
        var client = await _factory.LoginAsync("admin@bisconsultants.local");
        var uploaded = await AiFillUpload.PostAsync(client, EmptyPagePdf(), "once.pdf", "Once");
        uploaded.StatusCode.Should().Be(HttpStatusCode.OK);
        var id = (await uploaded.ReadJsonAsync()).GetProperty("id").GetGuid();

        var first = client.PostAsync($"/api/work-items/{id}/ai-fill", null);
        var second = client.PostAsync($"/api/work-items/{id}/ai-fill", null);
        (await first).StatusCode.Should().Be(HttpStatusCode.OK);
        (await second).StatusCode.Should().Be(HttpStatusCode.OK);
        await AiFillUpload.WaitForScanAsync(client, id);
        ScriptedOpenAiCompletions.Calls.Should().Be(1);
    }

    [Fact]
    public async Task Failed_model_does_not_roll_back_upload_and_manual_retry_remains()
    {
        ScriptedOpenAiCompletions.Reset();
        ScriptedOpenAiCompletions.Throw = new InvalidOperationException("model exploded");
        var client = await _factory.LoginAsync("admin@bisconsultants.local");
        var uploaded = await AiFillUpload.PostAsync(client, EmptyPagePdf(), "fail-scan.pdf", "Fail scan");
        uploaded.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await uploaded.ReadJsonAsync();
        var id = body.GetProperty("id").GetGuid();
        body.GetProperty("title").GetString().Should().Be("Fail scan");

        var after = await AiFillUpload.WaitForScanAsync(client, id);
        after.GetProperty("aiScan").GetProperty("status").GetString().Should().Be("failed");
        after.GetProperty("aiScan").GetProperty("message").GetString().Should().Contain("AI scan failed");
        after.GetProperty("title").GetString().Should().Be("Fail scan");
        after.GetRawText().Should().NotContain("no extractable text");

        ScriptedOpenAiCompletions.Throw = null;
        ScriptedOpenAiCompletions.ResponseJson = ScriptedOpenAiCompletions.JsonWithDifficulty("Easy", "Lot-and-block plat with one parcel.");
        var retry = await client.PostAsync($"/api/work-items/{id}/ai-fill", null);
        retry.StatusCode.Should().Be(HttpStatusCode.OK);
        var filled = await retry.ReadJsonAsync();
        filled.GetProperty("fields").GetProperty("title").GetProperty("present").GetBoolean().Should().BeTrue();
        var succeeded = await (await client.GetAsync($"/api/work-items/{id}")).ReadJsonAsync();
        succeeded.GetProperty("aiScan").GetProperty("status").GetString().Should().Be("succeeded");
        succeeded.GetProperty("title").GetString().Should().Be("Fail scan");
    }

    private static void AssertEmptyPropertyIds(System.Text.Json.JsonElement field)
    {
        field.GetProperty("present").GetBoolean().Should().BeFalse();
        if (!field.TryGetProperty("value", out var value))
        {
            return;
        }

        if (value.ValueKind is System.Text.Json.JsonValueKind.Null or System.Text.Json.JsonValueKind.Undefined)
        {
            return;
        }

        value.GetString().Should().BeNullOrEmpty();
    }

    private static byte[] EmptyPagePdf()
    {
        var sb = new StringBuilder();
        sb.Append("%PDF-1.4\n");
        var offsets = new List<int>();
        void Obj(string body)
        {
            offsets.Add(sb.Length);
            sb.Append(body);
            if (!body.EndsWith('\n')) sb.Append('\n');
        }

        Obj("1 0 obj << /Type /Catalog /Pages 2 0 R >> endobj\n");
        Obj("2 0 obj << /Type /Pages /Kids [3 0 R] /Count 1 >> endobj\n");
        Obj("3 0 obj << /Type /Page /Parent 2 0 R /MediaBox [0 0 612 792] >> endobj\n");
        var xref = sb.Length;
        sb.Append($"xref\n0 {offsets.Count + 1}\n0000000000 65535 f \n");
        foreach (var offset in offsets)
        {
            sb.Append($"{offset:D10} 00000 n \n");
        }

        sb.Append($"trailer << /Size {offsets.Count + 1} /Root 1 0 R >>\nstartxref\n{xref}\n%%EOF\n");
        return Encoding.ASCII.GetBytes(sb.ToString());
    }

    private static byte[] ImageOnlyPdf()
    {
        var pixels = new byte[8 * 8 * 3];
        for (var i = 0; i < pixels.Length; i += 3)
        {
            pixels[i] = 0x20;
            pixels[i + 1] = 0x20;
            pixels[i + 2] = 0x20;
        }

        var content = "q 200 0 0 200 0 0 cm /Im0 Do Q";
        var sb = new StringBuilder();
        sb.Append("%PDF-1.4\n");
        var offsets = new List<int>();
        void Obj(string body)
        {
            offsets.Add(sb.Length);
            sb.Append(body);
            if (!body.EndsWith('\n')) sb.Append('\n');
        }

        Obj("1 0 obj << /Type /Catalog /Pages 2 0 R >> endobj\n");
        Obj("2 0 obj << /Type /Pages /Kids [3 0 R] /Count 1 >> endobj\n");
        Obj("3 0 obj << /Type /Page /Parent 2 0 R /MediaBox [0 0 200 200] /Resources << /XObject << /Im0 4 0 R >> >> /Contents 5 0 R >> endobj\n");
        offsets.Add(sb.Length);
        sb.Append($"4 0 obj << /Type /XObject /Subtype /Image /Width 8 /Height 8 /ColorSpace /DeviceRGB /BitsPerComponent 8 /Length {pixels.Length} >> stream\n");
        sb.Append(Encoding.Latin1.GetString(pixels));
        sb.Append("\nendstream endobj\n");
        Obj($"5 0 obj << /Length {content.Length} >> stream\n{content}\nendstream endobj\n");

        var xref = sb.Length;
        sb.Append($"xref\n0 {offsets.Count + 1}\n0000000000 65535 f \n");
        foreach (var offset in offsets)
        {
            sb.Append($"{offset:D10} 00000 n \n");
        }

        sb.Append($"trailer << /Size {offsets.Count + 1} /Root 1 0 R >>\nstartxref\n{xref}\n%%EOF\n");
        return Encoding.Latin1.GetBytes(sb.ToString());
    }
}

public sealed class AiFillConfiguredFactory : ApiFactory
{
    protected override void ExtraConfig(Dictionary<string, string?> config)
    {
        config["AzureOpenAI:Endpoint"] = "https://oai-bis-deed-ai.openai.azure.com/";
        config["AzureOpenAI:ApiKey"] = "test-key-not-used";
        config["AzureOpenAI:Deployment"] = "gpt-4.1-mini";
    }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        base.ConfigureWebHost(builder);
        builder.ConfigureServices(services =>
        {
            services.AddSingleton<IAzureOpenAiCompletions, ScriptedOpenAiCompletions>();
        });
    }
}

public sealed class ScriptedOpenAiCompletions : IAzureOpenAiCompletions
{
    public static int Calls;
    public static int VisionCalls;
    public static int LastImageCount;
    public static int DelayMs;
    public static Exception? Throw;
    public static string? LastSystemPrompt;
    public static string ResponseJson = JsonWithDifficulty("Easy", "Lot-and-block plat with one parcel.");

    public bool IsConfigured => true;
    public string Deployment => "gpt-4.1-mini";

    public static void Reset()
    {
        Calls = 0;
        VisionCalls = 0;
        LastImageCount = 0;
        DelayMs = 0;
        Throw = null;
        LastSystemPrompt = null;
        ResponseJson = JsonWithDifficulty("Easy", "Lot-and-block plat with one parcel.");
    }

    public Task<string> CompleteJsonAsync(string systemPrompt, string userPrompt, CancellationToken cancellationToken = default) =>
        CompleteJsonAsync(systemPrompt, userPrompt, [], cancellationToken);

    public async Task<string> CompleteJsonAsync(
        string systemPrompt,
        string userPrompt,
        IReadOnlyList<AiFillVisionImage> images,
        CancellationToken cancellationToken = default)
    {
        if (DelayMs > 0)
        {
            await Task.Delay(DelayMs, cancellationToken);
        }

        if (Throw is not null)
        {
            throw Throw;
        }

        Interlocked.Increment(ref Calls);
        LastSystemPrompt = systemPrompt;
        LastImageCount = images.Count;
        if (images.Count > 0)
        {
            Interlocked.Increment(ref VisionCalls);
            images.Should().OnlyContain(image => image.Bytes.Length > 0);
        }

        systemPrompt.Should().Contain("difficulty");
        systemPrompt.Should().Contain("lot-block");
        systemPrompt.Should().Contain("easements");
        systemPrompt.Should().Contain("manual");
        systemPrompt.Should().Contain("Do not extract or return propertyIds");
        return ResponseJson;
    }

    public static string JsonWithDifficulty(string band, string why) =>
        $$"""
            {
              "overallConfidence": 0.84,
              "title": { "present": true, "value": "N-14-042 Final Plat", "confidence": 0.91 },
              "type": { "present": true, "value": "Plat", "confidence": 0.93 },
              "propertyIds": { "present": true, "value": "R123\nR456", "confidence": 0.72 },
              "annexationCount": { "present": true, "value": 0, "confidence": 0.55 },
              "correctionCount": { "present": false, "value": null, "confidence": 0 },
              "deedCount": { "present": true, "value": 0, "confidence": 0.6 },
              "platCount": { "present": true, "value": 1, "confidence": 0.88 },
              "workedOn": { "present": true, "value": "2026-03-15", "confidence": 0.64 },
              "difficulty": { "band": "{{band}}", "why": "{{why}}", "reasons": ["{{why}}"] }
            }
            """;

    public static string JsonWithoutDifficulty() =>
        """
            {
              "overallConfidence": 0.84,
              "title": { "present": true, "value": "N-14-042 Final Plat", "confidence": 0.91 },
              "type": { "present": true, "value": "Plat", "confidence": 0.93 },
              "propertyIds": { "present": true, "value": "R123\nR456", "confidence": 0.72 },
              "annexationCount": { "present": true, "value": 0, "confidence": 0.55 },
              "correctionCount": { "present": false, "value": null, "confidence": 0 },
              "deedCount": { "present": true, "value": 0, "confidence": 0.6 },
              "platCount": { "present": true, "value": 1, "confidence": 0.88 },
              "workedOn": { "present": true, "value": "2026-03-15", "confidence": 0.64 }
            }
            """;

    public static string CadMapDumpJson()
    {
        var ids = string.Join(", ", Enumerable.Range(11402, 22).Select(n => $"\"{n}\""));
        return $$"""
            {
              "overallConfidence": 0.95,
              "title": { "present": true, "value": "CORRECTION CR 315A", "confidence": 0.9 },
              "type": { "present": true, "value": "Other", "confidence": 0.7 },
              "propertyIds": { "present": true, "value": [{{ids}}], "confidence": 0.95 },
              "annexationCount": { "present": true, "value": 0, "confidence": 0.4 },
              "correctionCount": { "present": true, "value": 1, "confidence": 0.8 },
              "deedCount": { "present": true, "value": 0, "confidence": 0.4 },
              "platCount": { "present": true, "value": 0, "confidence": 0.4 },
              "workedOn": { "present": false, "value": null, "confidence": 0 },
              "difficulty": { "band": "Medium", "why": "Many map labels; subject PIDs ambiguous.", "reasons": ["Many map labels; subject PIDs ambiguous."] }
            }
            """;
    }

    public static string JsonWithPropertyIds(string value) =>
        $$"""
            {
              "overallConfidence": 0.84,
              "title": { "present": true, "value": "N-14-042 Final Plat", "confidence": 0.91 },
              "type": { "present": true, "value": "Plat", "confidence": 0.93 },
              "propertyIds": { "present": true, "value": {{System.Text.Json.JsonSerializer.Serialize(value)}}, "confidence": 0.9 },
              "annexationCount": { "present": true, "value": 0, "confidence": 0.55 },
              "correctionCount": { "present": false, "value": null, "confidence": 0 },
              "deedCount": { "present": true, "value": 0, "confidence": 0.6 },
              "platCount": { "present": true, "value": 1, "confidence": 0.88 },
              "workedOn": { "present": true, "value": "2026-03-15", "confidence": 0.64 },
              "difficulty": { "band": "Easy", "why": "Lot-and-block plat with one parcel.", "reasons": ["Lot-and-block plat with one parcel."] }
            }
            """;
}

public static class AiFillUpload
{
    public static async Task<HttpResponseMessage> PostAsync(
        HttpClient client,
        byte[] pdf,
        string fileName,
        string title)
    {
        using var form = new MultipartFormDataContent();
        form.Add(new StringContent(SeedIds.DemoClient.ToString()), "organizationId");
        form.Add(new StringContent(SeedIds.TypeDeed.ToString()), "documentTypeId");
        form.Add(new StringContent(title), "title");
        var file = new ByteArrayContent(pdf);
        file.Headers.ContentType = new MediaTypeHeaderValue("application/pdf");
        form.Add(file, "file", fileName);
        return await client.PostAsync("/api/work-items", form);
    }

    public static async Task<System.Text.Json.JsonElement> WaitForScanAsync(HttpClient client, Guid id)
    {
        System.Text.Json.JsonElement detail = default;
        for (var i = 0; i < 40; i++)
        {
            detail = await (await client.GetAsync($"/api/work-items/{id}")).ReadJsonAsync();
            var status = detail.TryGetProperty("aiScan", out var scan) && scan.ValueKind == System.Text.Json.JsonValueKind.Object
                ? scan.GetProperty("status").GetString()
                : null;
            if (status is not "pending" and not "running")
            {
                return detail;
            }

            await Task.Delay(250);
        }

        throw new InvalidOperationException($"AI scan did not finish. Last: {detail.GetRawText()}");
    }

    public static byte[] MinimalTextPdf()
    {
        var content = "BT /F1 12 Tf 72 720 Td (Demo Client Plat N-14-042) Tj ET";
        var sb = new StringBuilder();
        sb.Append("%PDF-1.4\n");
        var offsets = new List<int>();
        void Obj(string body)
        {
            offsets.Add(sb.Length);
            sb.Append(body);
            if (!body.EndsWith('\n')) sb.Append('\n');
        }

        Obj("1 0 obj << /Type /Catalog /Pages 2 0 R >> endobj\n");
        Obj("2 0 obj << /Type /Pages /Kids [3 0 R] /Count 1 >> endobj\n");
        Obj("3 0 obj << /Type /Page /Parent 2 0 R /MediaBox [0 0 612 792] /Contents 4 0 R /Resources << /Font << /F1 5 0 R >> >> >> endobj\n");
        Obj($"4 0 obj << /Length {content.Length} >> stream\n{content}\nendstream endobj\n");
        Obj("5 0 obj << /Type /Font /Subtype /Type1 /BaseFont /Helvetica >> endobj\n");
        var xref = sb.Length;
        sb.Append($"xref\n0 {offsets.Count + 1}\n0000000000 65535 f \n");
        foreach (var offset in offsets)
        {
            sb.Append($"{offset:D10} 00000 n \n");
        }

        sb.Append($"trailer << /Size {offsets.Count + 1} /Root 1 0 R >>\nstartxref\n{xref}\n%%EOF\n");
        return Encoding.ASCII.GetBytes(sb.ToString());
    }
}
