using System.Net.Http.Json;
using FluentAssertions;
using GisDashboard.Infrastructure.Persistence;

namespace GisDashboard.Tests;

public sealed class GisUi05DashboardKpisTests : IClassFixture<ApiFactory>
{
    private readonly ApiFactory _factory;

    public GisUi05DashboardKpisTests(ApiFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task Deadline_kpis_match_manage_documents_buckets_for_org_scope()
    {
        var client = await _factory.LoginAsync("admin@bisconsultants.local");

        await AssertScopeMatches(client, null);
        await AssertScopeMatches(client, SeedIds.DemoClient);
        await AssertScopeMatches(client, SeedIds.OtherClient);

        var all = Kpis(await (await client.GetAsync("/api/dashboard")).ReadJsonAsync());
        var demo = Kpis(await (await client.GetAsync($"/api/dashboard?organizationId={SeedIds.DemoClient}")).ReadJsonAsync());
        var other = Kpis(await (await client.GetAsync($"/api/dashboard?organizationId={SeedIds.OtherClient}")).ReadJsonAsync());

        all["firstdeadline"].Should().Be(demo["firstdeadline"] + other["firstdeadline"]);
        all["finaldeadline"].Should().Be(demo["finaldeadline"] + other["finaldeadline"]);
        all["duethisweek"].Should().Be(demo["duethisweek"] + other["duethisweek"]);
        demo["firstdeadline"].Should().BeGreaterThan(0);
    }

    [Fact]
    public async Task Due_this_week_kpi_uses_the_same_needed_by_window_as_the_md_chip()
    {
        var editor = await _factory.LoginAsync("editor@bisconsultants.local");
        var neededBy = DateTimeOffset.UtcNow.Date.AddDays(2);
        var patched = await editor.PatchAsJsonAsync($"/api/work-items/{SeedIds.DemoSurvey}", new
        {
            isPriority = true,
            priorityNeededBy = neededBy
        });
        patched.EnsureSuccessStatusCode();

        var dash = Kpis(await (await editor.GetAsync(
            $"/api/dashboard?organizationId={SeedIds.DemoClient}")).ReadJsonAsync());
        var list = await (await editor.GetAsync(
            $"/api/work-items?organizationId={SeedIds.DemoClient}&pageSize=1")).ReadJsonAsync();
        dash["duethisweek"].Should().Be(list.GetProperty("buckets").GetProperty("dueThisWeek").GetInt32());
        dash["duethisweek"].Should().BeGreaterThan(0);
    }

    private static async Task AssertScopeMatches(HttpClient client, Guid? organizationId)
    {
        var suffix = organizationId is { } id ? $"organizationId={id}" : "";
        var dash = Kpis(await (await client.GetAsync($"/api/dashboard?{suffix}")).ReadJsonAsync());
        var list = await (await client.GetAsync($"/api/work-items?pageSize=1&{suffix}")).ReadJsonAsync();
        var buckets = list.GetProperty("buckets");
        dash["duethisweek"].Should().Be(buckets.GetProperty("dueThisWeek").GetInt32());
        dash["firstdeadline"].Should().Be(buckets.GetProperty("firstDeadline").GetInt32());
        dash["finaldeadline"].Should().Be(buckets.GetProperty("finalDeadline").GetInt32());
        dash.Keys.Should().Contain(["active", "pending", "completed", "priority"]);
    }

    private static Dictionary<string, int> Kpis(System.Text.Json.JsonElement json) =>
        json.GetProperty("kpis").EnumerateArray().ToDictionary(
            x => x.GetProperty("key").GetString()!,
            x => x.GetProperty("count").GetInt32());
}
