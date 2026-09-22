using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using StudyHub.IntegrationTests.Infrastructure;
using Xunit.Abstractions;

namespace StudyHub.IntegrationTests;

[Collection(IntegrationTestCollection.Name)]
public class CalibrationAndSecurityTests
{
    private readonly IntegrationTestFixture _fixture;
    private readonly ITestOutputHelper _output;

    public CalibrationAndSecurityTests(IntegrationTestFixture fixture, ITestOutputHelper output)
    {
        _fixture = fixture;
        _output = output;
    }

    [Fact]
    public async Task Me_AnyUser_ShouldIssueExactlyOneStatement()
    {
        // Arrange
        var user = await _fixture.Factory.RegisterAndLoginAsync();
        _fixture.StatementCounter.Reset();

        // Act
        var response = await user.Client.GetAsync("/api/auth/me");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        // Calibration: since M8.1 /me is one statement, the profile and the month's usage
        // sum together. A different number here means the counter is wrong, and every
        // other statement assertion in this suite would be measuring nothing
        _fixture.StatementCounter.Count.Should().Be(1);
    }

    [Fact]
    public async Task RegisterLoginMe_NewAccount_ShouldRoundTrip()
    {
        // Arrange
        var client = _fixture.Factory.CreateClient();
        var email = $"it-{Guid.NewGuid():N}@test.com";

        // Act
        var register = await client.PostAsJsonAsync("/api/auth/register", new
        {
            fullName = "Round Trip",
            email,
            password = ApiClient.Password,
            confirmPassword = ApiClient.Password
        });

        var login = await client.PostAsJsonAsync("/api/auth/login", new { email, password = ApiClient.Password });
        var token = (await login.ReadJsonAsync()).GetProperty("accessToken").GetString();

        client.DefaultRequestHeaders.Authorization = new("Bearer", token);
        var me = await client.GetAsync("/api/auth/me");

        // Assert
        register.StatusCode.Should().Be(HttpStatusCode.Created);
        login.StatusCode.Should().Be(HttpStatusCode.OK);
        me.StatusCode.Should().Be(HttpStatusCode.OK);

        (await me.ReadJsonAsync()).GetProperty("email").GetString().Should().Be(email);
    }

    [Fact]
    public async Task Dashboard_WithoutToken_ShouldReturn401()
    {
        // Arrange
        var client = _fixture.Factory.CreateClient();

        // Act
        var response = await client.GetAsync("/api/dashboard");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Root_Anonymous_ShouldServeTheDemoPage()
    {
        // Arrange
        var client = _fixture.Factory.CreateClient();

        // Act
        var page = await client.GetAsync("/");
        var api = await client.GetAsync("/api/dashboard");

        // Assert
        page.StatusCode.Should().Be(HttpStatusCode.OK);
        page.Content.Headers.ContentType!.MediaType.Should().Be("text/html");

        // The static files sit before authentication, and that must not have loosened the
        // fallback policy the API itself relies on (ADR-48)
        api.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task GetCourse_OfAnotherUser_ShouldReturn403()
    {
        // Arrange
        var owner = await _fixture.Factory.RegisterAndLoginAsync();
        var stranger = await _fixture.Factory.RegisterAndLoginAsync();

        var created = await owner.Client.PostAsJsonAsync("/api/courses",
            new { title = "Private course", description = (string?)null });
        var courseId = (await created.ReadJsonAsync()).GetProperty("courseId").GetGuid();

        // Act
        var response = await stranger.Client.GetAsync($"/api/courses/{courseId}/tree");

        // Assert
        // 403, not 404: the resource exists and the caller may not have it (ADR-33)
        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task Refresh_TwoParallelRequestsWithOneToken_ShouldNeverBothSucceed()
    {
        // Arrange
        var user = await _fixture.Factory.RegisterAndLoginAsync();
        var client = _fixture.Factory.CreateClient();

        var login = await client.PostAsJsonAsync("/api/auth/login",
            new { email = user.Email, password = ApiClient.Password });
        var refreshToken = (await login.ReadJsonAsync()).GetProperty("refreshToken").GetString();

        // Which defence refused the loser is timing, not a rule, so it is counted and
        // reported rather than asserted: requiring a minimum number of 409s would fail on
        // a machine that is merely faster or slower
        var lostTheRace = 0;
        var caughtAsReuse = 0;

        for (var round = 0; round < 10; round++)
        {
            // Act
            var first = client.PostAsJsonAsync("/api/auth/refresh", new { refreshToken });
            var second = client.PostAsJsonAsync("/api/auth/refresh", new { refreshToken });

            var responses = await Task.WhenAll(first, second);

            // Assert
            var winners = responses.Where(r => r.StatusCode == HttpStatusCode.OK).ToList();

            // Two winners would be the forked chain ADR-27 exists to prevent: two live
            // refresh tokens from one, either of which could keep refreshing for ever
            winners.Should().HaveCount(1, "exactly one of two parallel refreshes may rotate the token");

            // The loser either lost the xmin race (409) or arrived after the rotation and
            // was read as reuse (401, §9.3). Both are refusals; nothing else is acceptable
            var loser = responses.Single(r => r.StatusCode != HttpStatusCode.OK);
            loser.StatusCode.Should().BeOneOf(HttpStatusCode.Conflict, HttpStatusCode.Unauthorized);

            if (loser.StatusCode == HttpStatusCode.Conflict)
                lostTheRace++;
            else
                caughtAsReuse++;

            refreshToken = (await winners[0].ReadJsonAsync()).GetProperty("refreshToken").GetString();

            // A 401 loser means reuse detection fired, and by design that revokes every
            // session the user has (§9.3) — including the token this round just won. The
            // next round therefore starts from a fresh login rather than a dead chain
            if (loser.StatusCode == HttpStatusCode.Unauthorized)
            {
                var freshLogin = await client.PostAsJsonAsync("/api/auth/login",
                    new { email = user.Email, password = ApiClient.Password });
                refreshToken = (await freshLogin.ReadJsonAsync()).GetProperty("refreshToken").GetString();
            }
        }

        // 409 means the xmin concurrency token refused the second save (ADR-27); 401 means
        // the loser arrived after the rotation and was read as reuse (§9.3). The test says
        // which defence it actually exercised this time
        _output.WriteLine($"409 rounds: {lostTheRace}, 401 rounds: {caughtAsReuse}");
    }
}
