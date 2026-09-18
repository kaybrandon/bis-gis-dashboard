using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using FluentAssertions;
using GisDashboard.Domain;
using GisDashboard.Infrastructure.Persistence;

namespace GisDashboard.Tests;

/// <summary>
/// CR01 — Worked and Needed by stay independent (no auto-link / same-day sync).
/// Badge/display reads saved PriorityNeededBy, not WorkedOn.
/// </summary>
public sealed class Cr01WorkedNeededByTests : IClassFixture<ApiFactory>
{
    private readonly ApiFactory _factory;

    public Cr01WorkedNeededByTests(ApiFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public void Entity_setters_do_not_copy_worked_onto_needed_by_or_back()
    {
        var item = new WorkItem { IsPriority = true };
        var needed = new DateTimeOffset(2026, 10, 5, 0, 0, 0, TimeSpan.Zero);
        var worked = new DateTimeOffset(2026, 9, 1, 16, 0, 0, TimeSpan.Zero);

        item.SetPriorityNeededBy(needed);
        item.SetWorkedOn(worked);
        item.PriorityNeededBy!.Value.Date.Should().Be(needed.Date);
        item.WorkedOn.Should().Be(worked);

        var laterWorked = worked.AddDays(10);
        item.SetWorkedOn(laterWorked);
        item.WorkedOn.Should().Be(laterWorked);
        item.PriorityNeededBy!.Value.Date.Should().Be(needed.Date);

        var laterNeeded = new DateTimeOffset(2026, 10, 12, 0, 0, 0, TimeSpan.Zero);
        item.SetPriorityNeededBy(laterNeeded);
        item.PriorityNeededBy!.Value.Date.Should().Be(laterNeeded.Date);
        item.WorkedOn.Should().Be(laterWorked);
    }

    [Fact]
    public async Task Save_and_reload_keeps_worked_and_needed_by_independent()
    {
        var client = await _factory.LoginAsync("editor@bisconsultants.local");
        var id = await UploadAsync(client, "cr01-independent.pdf");

        var needed = new DateTimeOffset(2026, 10, 5, 0, 0, 0, TimeSpan.Zero);
        var worked = new DateTimeOffset(2026, 9, 1, 16, 0, 0, TimeSpan.Zero);
        var first = await client.PatchAsJsonAsync($"/api/work-items/{id}", new
        {
            isPriority = true,
            priorityNote = "board packet",
            priorityNeededBy = needed,
            workedOn = worked
        });
        first.StatusCode.Should().Be(HttpStatusCode.OK, await first.Content.ReadAsStringAsync());
        AssertDates(await first.ReadJsonAsync(), worked, needed);

        AssertDates(await (await client.GetAsync($"/api/work-items/{id}")).ReadJsonAsync(), worked, needed);

        var laterWorked = new DateTimeOffset(2026, 9, 18, 12, 0, 0, TimeSpan.Zero);
        var onlyWorked = await client.PatchAsJsonAsync($"/api/work-items/{id}", new { workedOn = laterWorked });
        onlyWorked.StatusCode.Should().Be(HttpStatusCode.OK);
        AssertDates(await onlyWorked.ReadJsonAsync(), laterWorked, needed);
        AssertDates(await (await client.GetAsync($"/api/work-items/{id}")).ReadJsonAsync(), laterWorked, needed);

        var laterNeeded = new DateTimeOffset(2026, 10, 12, 0, 0, 0, TimeSpan.Zero);
        var onlyNeeded = await client.PatchAsJsonAsync($"/api/work-items/{id}", new { priorityNeededBy = laterNeeded });
        onlyNeeded.StatusCode.Should().Be(HttpStatusCode.OK);
        AssertDates(await onlyNeeded.ReadJsonAsync(), laterWorked, laterNeeded);
        AssertDates(await (await client.GetAsync($"/api/work-items/{id}")).ReadJsonAsync(), laterWorked, laterNeeded);
    }

    [Fact]
    public async Task List_badge_field_is_saved_needed_by_not_worked()
    {
        var client = await _factory.LoginAsync("editor@bisconsultants.local");
        var id = await UploadAsync(client, "cr01-badge.pdf");

        var needed = new DateTimeOffset(2026, 10, 5, 0, 0, 0, TimeSpan.Zero);
        var worked = new DateTimeOffset(2026, 9, 1, 16, 0, 0, TimeSpan.Zero);
        (await client.PatchAsJsonAsync($"/api/work-items/{id}", new
        {
            isPriority = true,
            priorityNote = "due for filing",
            priorityNeededBy = needed,
            workedOn = worked
        })).StatusCode.Should().Be(HttpStatusCode.OK);

        var list = await (await client.GetAsync("/api/work-items?pageSize=100")).ReadJsonAsync();
        var row = list.GetProperty("items").EnumerateArray()
            .Single(x => x.GetProperty("id").GetGuid() == id);

        DateTimeOffset.Parse(row.GetProperty("priorityNeededBy").GetString()!).Date
            .Should().Be(needed.Date);
        DateTimeOffset.Parse(row.GetProperty("workedOn").GetString()!).Date
            .Should().Be(worked.Date);
        row.GetProperty("priorityNeededBy").GetString()
            .Should().NotBe(row.GetProperty("workedOn").GetString());
    }

    [Fact]
    public async Task Completing_does_not_sync_needed_by_to_worked_or_same_day()
    {
        var client = await _factory.LoginAsync("editor@bisconsultants.local");
        var id = await UploadAsync(client, "cr01-complete.pdf");

        var needed = new DateTimeOffset(2026, 10, 5, 0, 0, 0, TimeSpan.Zero);
        (await client.PatchAsJsonAsync($"/api/work-items/{id}", new
        {
            isPriority = true,
            priorityNote = "complete without copying dates",
            priorityNeededBy = needed
        })).StatusCode.Should().Be(HttpStatusCode.OK);

        var completed = await client.PatchAsJsonAsync($"/api/work-items/{id}", new
        {
            statusId = SeedIds.StatusWorked
        });
        completed.StatusCode.Should().Be(HttpStatusCode.OK);
        var detail = await completed.ReadJsonAsync();

        DateTimeOffset.Parse(detail.GetProperty("priorityNeededBy").GetString()!).Date
            .Should().Be(needed.Date);
        detail.GetProperty("workedOn").ValueKind.Should().NotBe(System.Text.Json.JsonValueKind.Null);
        DateTimeOffset.Parse(detail.GetProperty("workedOn").GetString()!).Date
            .Should().NotBe(needed.Date);
    }

    private static void AssertDates(System.Text.Json.JsonElement json, DateTimeOffset worked, DateTimeOffset needed)
    {
        DateTimeOffset.Parse(json.GetProperty("workedOn").GetString()!).Date.Should().Be(worked.Date);
        DateTimeOffset.Parse(json.GetProperty("priorityNeededBy").GetString()!).Date.Should().Be(needed.Date);
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
