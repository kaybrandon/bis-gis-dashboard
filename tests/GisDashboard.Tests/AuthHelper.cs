using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;

namespace GisDashboard.Tests;

internal static class AuthHelper
{
    private static readonly JsonSerializerOptions Json = new() { PropertyNameCaseInsensitive = true };

    public static async Task<HttpClient> LoginAsync(this ApiFactory factory, string email)
    {
        var client = factory.CreateClient();
        var response = await client.PostAsJsonAsync("/api/auth/login", new { email, password = factory.DemoPassword });
        if (!response.IsSuccessStatusCode)
        {
            throw new HttpRequestException($"Login {email} failed ({(int)response.StatusCode}): {await response.Content.ReadAsStringAsync()}");
        }
        using var doc = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        var token = doc.RootElement.GetProperty("token").GetString();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        return client;
    }

    public static async Task<JsonElement> ReadJsonAsync(this HttpResponseMessage response)
    {
        var text = await response.Content.ReadAsStringAsync();
        return JsonDocument.Parse(string.IsNullOrWhiteSpace(text) ? "{}" : text).RootElement.Clone();
    }

    public static JsonSerializerOptions Options => Json;
}
