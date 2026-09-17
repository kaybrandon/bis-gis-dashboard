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
        json.GetProperty("fields").GetProperty("propertyIds").GetProperty("value").GetString().Should().Contain("R123");
        json.GetProperty("fields").GetProperty("platCount").GetProperty("value").GetInt32().Should().Be(1);
        json.GetProperty("fields").GetProperty("workedOn").GetProperty("value").GetString().Should().Be("2026-03-15");

        var after = await (await client.GetAsync($"/api/work-items/{SeedIds.DemoPlat}")).ReadJsonAsync();
        after.GetProperty("title").GetString().Should().Be(originalTitle);
        after.GetProperty("statusId").GetGuid().Should().Be(originalStatus);
        after.GetProperty("assignedToUserId").GetGuid().Should().Be(originalAssignee);
        after.GetProperty("isSplit").GetBoolean().Should().Be(before.GetProperty("isSplit").GetBoolean());
        after.GetProperty("isSketch").GetBoolean().Should().Be(before.GetProperty("isSketch").GetBoolean());
        after.GetProperty("isPriority").GetBoolean().Should().Be(before.GetProperty("isPriority").GetBoolean());
        after.GetProperty("isReviewed").GetBoolean().Should().Be(before.GetProperty("isReviewed").GetBoolean());
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
    public async Task Empty_text_pdf_is_rejected_before_model()
    {
        var client = await _factory.LoginAsync("admin@bisconsultants.local");
        using var form = new MultipartFormDataContent();
        form.Add(new StringContent(SeedIds.DemoClient.ToString()), "organizationId");
        form.Add(new StringContent(SeedIds.TypeDeed.ToString()), "documentTypeId");
        form.Add(new StringContent("Empty scan"), "title");
        var file = new ByteArrayContent(EmptyPagePdf());
        file.Headers.ContentType = new MediaTypeHeaderValue("application/pdf");
        form.Add(file, "file", "empty-scan.pdf");
        var uploaded = await client.PostAsync("/api/work-items", form);
        uploaded.StatusCode.Should().Be(HttpStatusCode.OK);
        var id = (await uploaded.ReadJsonAsync()).GetProperty("id").GetGuid();

        ScriptedOpenAiCompletions.Calls = 0;
        var response = await client.PostAsync($"/api/work-items/{id}/ai-fill", null);
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await response.ReadJsonAsync()).GetProperty("message").GetString().Should().Contain("no extractable text");
        ScriptedOpenAiCompletions.Calls.Should().Be(0);
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

    public bool IsConfigured => true;
    public string Deployment => "gpt-4.1-mini";

    public Task<string> CompleteJsonAsync(string systemPrompt, string userPrompt, CancellationToken cancellationToken = default)
    {
        Interlocked.Increment(ref Calls);
        const string json = """
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
        return Task.FromResult(json);
    }
}
