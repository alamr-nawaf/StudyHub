using System.Globalization;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;

namespace StudyHub.IntegrationTests.Infrastructure;

/// <summary>
/// One account and its HTTP client. Every test creates its own, with a unique e-mail, so
/// no test can depend on — or be broken by — another test's data (G5).
/// </summary>
public sealed record TestUser(Guid Id, string Email, string AccessToken, HttpClient Client);

/// <summary>
/// The small helpers the integration tests share: registering an account, logging in, and
/// reading JSON out of a response.
/// </summary>
public static class ApiClient
{
    public const string Password = "Password123";

    public static async Task<TestUser> RegisterAndLoginAsync(this StudyHubApiFactory factory)
    {
        var client = factory.CreateClient();
        var email = $"it-{Guid.NewGuid():N}@test.com";

        var register = await client.PostAsJsonAsync("/api/auth/register", new
        {
            fullName = "Integration Test",
            email,
            password = Password,
            confirmPassword = Password
        });
        register.EnsureSuccessStatusCode();

        var userId = (await register.ReadJsonAsync()).GetProperty("userId").GetGuid();
        var token = await LoginAsync(client, email);

        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

        return new TestUser(userId, email, token, client);
    }

    public static async Task<string> LoginAsync(HttpClient client, string email)
    {
        var login = await client.PostAsJsonAsync("/api/auth/login", new { email, password = Password });
        login.EnsureSuccessStatusCode();

        return (await login.ReadJsonAsync()).GetProperty("accessToken").GetString()!;
    }

    public static async Task<JsonElement> ReadJsonAsync(this HttpResponseMessage response) =>
        JsonDocument.Parse(await response.Content.ReadAsStringAsync()).RootElement.Clone();

    /// <summary>
    /// ISO 8601 UTC ending in Z, the only shape the API accepts (§14.1). InvariantCulture is
    /// not decoration: on a machine whose culture uses the Umm al-Qura calendar, the same
    /// format string yields a Hijri year — 1448 instead of 2026 — and the API accepts it,
    /// because a date far in the past is a valid date.
    /// </summary>
    public static string Iso(this DateTime utc) =>
        utc.ToString("yyyy-MM-ddTHH:mm:ss", CultureInfo.InvariantCulture) + "Z";
}
