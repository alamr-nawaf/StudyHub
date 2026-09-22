using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using FluentAssertions;
using StudyHub.IntegrationTests.Infrastructure;

namespace StudyHub.IntegrationTests;

[Collection(IntegrationTestCollection.Name)]
public class RateLimitTests
{
    private readonly IntegrationTestFixture _fixture;

    public RateLimitTests(IntegrationTestFixture fixture) => _fixture = fixture;

    [Fact]
    public async Task Login_OverTheAuthLimit_ShouldReturn429WithRetryAfter()
    {
        // Arrange
        var user = await _fixture.Factory.RegisterAndLoginAsync();
        var client = _fixture.LowRateLimitFactory.CreateClient();

        // Act
        var responses = new List<HttpResponseMessage>();
        for (var i = 0; i < IntegrationTestFixture.LowRateLimit + 1; i++)
        {
            responses.Add(await client.PostAsJsonAsync("/api/auth/login",
                new { email = user.Email, password = ApiClient.Password }));
        }

        // Assert
        // The limit counts requests, not failures: the first three succeed and the fourth
        // is refused inside the same window (ADR-45)
        responses.Take(IntegrationTestFixture.LowRateLimit)
            .Should().OnlyContain(r => r.StatusCode == HttpStatusCode.OK);

        var refused = responses[^1];
        refused.StatusCode.Should().Be(HttpStatusCode.TooManyRequests);

        // Retry-After, so a client knows when to come back instead of hammering
        refused.Headers.RetryAfter.Should().NotBeNull();

        var body = await refused.ReadJsonAsync();
        body.GetProperty("status").GetInt32().Should().Be(429);
        body.GetProperty("title").GetString().Should().Be("Too many requests.");
    }

    [Fact]
    public async Task AiLimit_OneUserSpent_ShouldNotLimitAnother()
    {
        // Arrange — the accounts are created on the ordinary host, so their register and
        // login requests are not charged to the low host's auth budget
        var spender = await _fixture.Factory.RegisterAndLoginAsync();
        var bystander = await _fixture.Factory.RegisterAndLoginAsync();

        var spenderNote = await CreateNoteAsync(spender);
        var bystanderNote = await CreateNoteAsync(bystander);

        var spenderClient = Authenticated(spender);
        var bystanderClient = Authenticated(bystander);

        // Act
        var spenderResponses = new List<HttpResponseMessage>();
        for (var i = 0; i < IntegrationTestFixture.LowRateLimit + 1; i++)
        {
            spenderResponses.Add(
                await spenderClient.PostAsJsonAsync($"/api/notes/{spenderNote}/summarize", new { }));
        }

        var bystanderResponse =
            await bystanderClient.PostAsJsonAsync($"/api/notes/{bystanderNote}/summarize", new { });

        // Assert
        spenderResponses.Take(IntegrationTestFixture.LowRateLimit)
            .Should().OnlyContain(r => r.StatusCode == HttpStatusCode.OK);
        spenderResponses[^1].StatusCode.Should().Be(HttpStatusCode.TooManyRequests);

        // The partition is the caller, not the process: this fails if UseRateLimiter sits
        // before authentication, because then every AI caller shares one partition (ADR-45)
        bystanderResponse.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    private HttpClient Authenticated(TestUser user)
    {
        var client = _fixture.LowRateLimitFactory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", user.AccessToken);

        return client;
    }

    private static async Task<Guid> CreateNoteAsync(TestUser user)
    {
        var response = await user.Client.PostAsJsonAsync("/api/notes", new
        {
            title = "Rate limit note",
            content = "Something short to summarize.",
            parentItemId = (Guid?)null,
            courseId = (Guid?)null
        });
        response.StatusCode.Should().Be(HttpStatusCode.Created);

        return (await response.ReadJsonAsync()).GetProperty("noteId").GetGuid();
    }
}
