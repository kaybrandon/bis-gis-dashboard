using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using FluentAssertions;
using GisDashboard.Infrastructure.Persistence;

namespace GisDashboard.Tests;

/// <summary>
/// CR06 — Reviewed Yes/No persists independently of Hours.
/// Total time is the sum of time entries on that document (separate column).
/// </summary>
public sealed class Cr06ReviewedTotalTimeTests : IClassFixture<ApiFactory>
{
    private readonly ApiFactory _factory;

    public Cr06ReviewedTotalTimeTests(ApiFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task Reviewed_toggle_persists_on_save_and_reload_and_list()
    {
        var client = await _factory.LoginAsync("editor@bisconsultants.local");
        var id = await UploadAsync(client, "cr06-reviewed.pdf");

        var marked = await client.PatchAsJsonAsync($"/api/work-items/{id}", new { isReviewed = true });
        marked.StatusCode.Should().Be(HttpStatusCode.OK);
        (await marked.ReadJsonAsync()).GetProperty("isReviewed").GetBoolean().Should().BeTrue();

        var reloaded = await (await client.GetAsync($"/api/work-items/{id}")).ReadJsonAsync();
        reloaded.GetProperty("isReviewed").GetBoolean().Should().BeTrue();

        var list = await (await client.GetAsync("/api/work-items?pageSize=100")).ReadJsonAsync();
        var row = list.GetProperty("items").EnumerateArray()
            .Single(x => x.GetProperty("id").GetGuid() == id);
        row.GetProperty("isReviewed").GetBoolean().Should().BeTrue();
        row.TryGetProperty("hoursLabel", out _).Should().BeTrue();

        var cleared = await client.PatchAsJsonAsync($"/api/work-items/{id}", new { isReviewed = false });
        cleared.StatusCode.Should().Be(HttpStatusCode.OK);
        (await (await client.GetAsync($"/api/work-items/{id}")).ReadJsonAsync())
            .GetProperty("isReviewed").GetBoolean().Should().BeFalse();
    }

    [Fact]
    public async Task Total_time_matches_sum_of_time_entries_and_is_independent_of_reviewed()
    {
        var client = await _factory.LoginAsync("editor@bisconsultants.local");
        var id = await UploadAsync(client, "cr06-total-time.pdf");

        var empty = await (await client.GetAsync($"/api/work-items/{id}")).ReadJsonAsync();
        empty.GetProperty("isReviewed").GetBoolean().Should().BeFalse();
        empty.GetProperty("hours").GetDecimal().Should().Be(0);
        empty.GetProperty("hoursLabel").GetString().Should().Be("0m");

        (await client.PostAsJsonAsync($"/api/work-items/{id}/time-entries", new
        {
            hours = 1,
            minutes = 15,
            note = "CR06 first entry"
        })).StatusCode.Should().Be(HttpStatusCode.Created);
        (await client.PostAsJsonAsync($"/api/work-items/{id}/time-entries", new
        {
            hours = 0,
            minutes = 30,
            note = "CR06 second entry"
        })).StatusCode.Should().Be(HttpStatusCode.Created);

        var afterEntries = await (await client.GetAsync($"/api/work-items/{id}")).ReadJsonAsync();
        afterEntries.GetProperty("hours").GetDecimal().Should().Be(1.75m);
        afterEntries.GetProperty("hoursLabel").GetString().Should().Be("1h 45m");
        afterEntries.GetProperty("isReviewed").GetBoolean().Should().BeFalse();

        var log = await (await client.GetAsync($"/api/work-items/{id}/time-entries")).ReadJsonAsync();
        log.GetProperty("totalMinutes").GetInt32().Should().Be(105);
        log.GetProperty("totalLabel").GetString().Should().Be(afterEntries.GetProperty("hoursLabel").GetString());

        (await client.PatchAsJsonAsync($"/api/work-items/{id}", new { isReviewed = true }))
            .StatusCode.Should().Be(HttpStatusCode.OK);

        var afterReview = await (await client.GetAsync($"/api/work-items/{id}")).ReadJsonAsync();
        afterReview.GetProperty("isReviewed").GetBoolean().Should().BeTrue();
        afterReview.GetProperty("hoursLabel").GetString().Should().Be("1h 45m");

        var list = await (await client.GetAsync("/api/work-items?pageSize=100")).ReadJsonAsync();
        var row = list.GetProperty("items").EnumerateArray()
            .Single(x => x.GetProperty("id").GetGuid() == id);
        row.GetProperty("isReviewed").GetBoolean().Should().BeTrue();
        row.GetProperty("hoursLabel").GetString().Should().Be("1h 45m");
        row.GetProperty("hours").GetDecimal().Should().Be(1.75m);
    }

    [Fact]
    public async Task Seed_demo_plat_total_time_matches_known_entries()
    {
        var client = await _factory.LoginAsync("editor@bisconsultants.local");
        var detail = await (await client.GetAsync($"/api/work-items/{SeedIds.DemoPlat}")).ReadJsonAsync();
        var log = await (await client.GetAsync($"/api/work-items/{SeedIds.DemoPlat}/time-entries")).ReadJsonAsync();

        log.GetProperty("totalMinutes").GetInt32().Should().Be(135);
        log.GetProperty("totalLabel").GetString().Should().Be("2h 15m");
        detail.GetProperty("hoursLabel").GetString().Should().Be(log.GetProperty("totalLabel").GetString());
        detail.GetProperty("hours").GetDecimal().Should().Be(2.25m);

        var list = await (await client.GetAsync("/api/work-items?pageSize=100")).ReadJsonAsync();
        var plat = list.GetProperty("items").EnumerateArray()
            .Single(x => x.GetProperty("id").GetGuid() == SeedIds.DemoPlat);
        plat.GetProperty("hoursLabel").GetString().Should().Be("2h 15m");
        plat.TryGetProperty("isReviewed", out _).Should().BeTrue();
    }

    [Fact]
    public async Task Settings_keep_reviewed_and_separate_total_time()
    {
        var client = await _factory.LoginAsync("admin@bisconsultants.local");
        var settings = await (await client.GetAsync("/api/settings")).ReadJsonAsync();
        var note = settings.GetProperty("features").GetProperty("reviewed").GetProperty("note").GetString();
        note.Should().Contain("Total time");
        note.Should().Contain("sum of time entries");
        note.Should().NotContain("instead of Hours");
    }

    private static async Task<Guid> UploadAsync(HttpClient client, string fileName)
    {
        using var form = new MultipartFormDataContent();
        form.Add(new StringContent(SeedIds.DemoClient.ToString()), "organizationId");
        form.Add(new StringContent("Demo Client"), "organizationName");
        form.Add(new StringContent(SeedIds.TypeDeed.ToString()), "documentTypeId");
        form.Add(new StringContent("Deed"), "documentTypeName");
        form.Add(new StringContent(fileName), "title");
        var file = new ByteArrayContent([0x25, 0x50, 0x44, 0x46, 0x2D]);
        file.Headers.ContentType = new MediaTypeHeaderValue("application/pdf");
        form.Add(file, "file", fileName);
        var uploaded = await client.PostAsync("/api/work-items", form);
        uploaded.StatusCode.Should().Be(HttpStatusCode.OK, await uploaded.Content.ReadAsStringAsync());
        return (await uploaded.ReadJsonAsync()).GetProperty("id").GetGuid();
    }
}
